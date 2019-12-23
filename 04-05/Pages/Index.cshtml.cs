using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using System.Linq;
using Markdig;
using Knote.Models;
using System.Collections.Generic;
using Minio;

namespace Knote.Pages
{
	public class IndexModel : PageModel
	{
		private readonly IWebHostEnvironment _environment;
		private IConfigurationRoot _config;
		private IMongoCollection<NoteModel> _db;
		private MinioClient _minioClient;
		private string _minioBucket = "image-storage";

		[BindProperty]
		public IFormFile UploadedFile { get; set; }

		[BindProperty]
		public string NoteContent { get; set; }

		[TempData]
		public string TempNoteContent { get; set; }

		[ViewData]
		public List<NoteModelContext> Notes { get; set; }

		public IndexModel(IWebHostEnvironment environment)
		{
			_environment = environment;
			_config = new ConfigurationBuilder()
				.AddEnvironmentVariables()
				.Build();

			InitMongo().Wait();
			InitMinIO().Wait();
		}

		private async Task InitMongo()
		{
			Console.WriteLine("Initialising MongoDB...");
			var success = false;

			while (!success)
			{
				try
				{
					var mongoURL = _config.GetSection("MONGO_URL").Value ?? "mongodb://localhost:27017/dev";
					var mongoURLBuilder = new MongoUrlBuilder(mongoURL);
					var client = new MongoClient(mongoURL);
					success = true;
					Console.WriteLine("MongoDB initialised");
					_db = client
									.GetDatabase(mongoURLBuilder.DatabaseName)
									.GetCollection<NoteModel>("notes");
				}
				catch (Exception)
				{
					Console.WriteLine("Error connecting to MongoDB, retrying in 1 second");
					await Task.Delay(1000);
				}
			}
		}

		private async Task InitMinIO()
		{
			Console.WriteLine("Initialising MinIO...");
			var minioHost = _config.GetSection("MINIO_HOST").Value ?? "localhost";
			var port = "9000";
			var accessKey = _config.GetSection("MINIO_ACCESS_KEY").Value;
			var secretKey = _config.GetSection("MINIO_SECRET_KEY").Value;

			var client = new MinioClient(endpoint: $"{minioHost}:{port}", accessKey: accessKey, secretKey: secretKey);
			var success = false;

			while (!success)
			{
				try
				{
					var bucketExists = await client.BucketExistsAsync(_minioBucket);
					if (!bucketExists)
					{
						await client.MakeBucketAsync(_minioBucket);
					}
					success = true;
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
					await Task.Delay(5000);
				}
			}

			Console.WriteLine("MinIO initialised");
			_minioClient = client;
		}

		public async Task OnGetAsync()
		{
			Notes = await RetrieveNotes(_db);
			NoteContent = TempNoteContent;
		}

		public async Task<IActionResult> OnGetImgAsync(string name)
		{
			byte[] file = {};
			await _minioClient.GetObjectAsync(_minioBucket, name, stream =>
			{
				using (var memoryStream = new MemoryStream())
				{
					stream.CopyTo(memoryStream);
					file = memoryStream.ToArray();
				}
			});

			return File(file, "application/octet-stream");
		}

		public async Task<IActionResult> OnPostUploadAsync()
		{
			using (var memoryStream = new MemoryStream())
			{
				await UploadedFile.CopyToAsync(memoryStream);
				if (memoryStream.CanSeek) memoryStream.Position = 0;
				await _minioClient.PutObjectAsync(
					_minioBucket,
					UploadedFile.FileName,
					memoryStream,
					memoryStream.Length
				);
			}

			var link = $"?handler=img&name={Uri.EscapeUriString(UploadedFile.FileName)}";
			TempNoteContent = $"{NoteContent} ![]({link})";
			Notes = await RetrieveNotes(_db);
			return RedirectToPage("/Index");
		}

		public async Task<IActionResult> OnPostSubmitAsync()
		{
			var newNote = new NoteModel()
			{
				Description = NoteContent
			};
			await SaveNote(_db, newNote);
			return RedirectToPage("/Index");
		}

		private async Task SaveNote(IMongoCollection<NoteModel> db, NoteModel note)
		{
			await db.InsertOneAsync(note);
		}

		private async Task<List<NoteModelContext>> RetrieveNotes(IMongoCollection<NoteModel> db)
		{
			var notesNormalOrder = await _db.FindAsync<NoteModel>(note => true);

			var notes = notesNormalOrder
				.ToEnumerable<NoteModel>()
				.Select(it => new NoteModelContext() { MarkedDescription = Markdown.ToHtml(it.Description) })
				.ToList();

			notes.Reverse();

			return notes;
		}

		public class NoteModelContext
		{
			public string MarkedDescription { get; set; }
		}
	}
}
