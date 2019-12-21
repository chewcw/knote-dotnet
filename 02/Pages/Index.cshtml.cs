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

namespace Knote.Pages
{
  public class IndexModel : PageModel
  {
    private readonly IWebHostEnvironment _environment;
    private IConfigurationRoot _config;
    private IMongoCollection<NoteModel> _db;

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

      InitMongo();
    }

    private void InitMongo()
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
          Task.Delay(1000).Wait();
        }
      }
    }

    public async Task OnGetAsync()
    {
      Notes = await RetrieveNotes(_db);
      NoteContent = TempNoteContent;
    }

    public async Task<IActionResult> OnPostUploadAsync()
    {
      if (!Directory.Exists(Path.Combine(_environment.WebRootPath, "uploads")))
      {
        Directory.CreateDirectory(Path.Combine(_environment.WebRootPath, "uploads"));
      }

      var filename = Guid.NewGuid().ToString();
      var file = Path.Combine(_environment.WebRootPath, "uploads", filename);
      using (var fileStream = new FileStream(file, FileMode.Create))
      {
        await UploadedFile.CopyToAsync(fileStream);
      }

      var link = $"/uploads/{Uri.EscapeUriString(filename)}";
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
