using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Knote.Models
{
  public class NoteModel
  {
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("description")]
    public string Description { get; set; }
  }
}