using MongoDB.Driver;
using Xllerate.Models.MongoDB;

namespace Xllerate.Services;

public class MongoDbContext
{
    private readonly IMongoDatabase _db;

    public MongoDbContext(IConfiguration config)
    {
        var settings = config.GetSection("MongoDB");
        var connectionString = settings["ConnectionString"];
        var dbName = settings["DatabaseName"];

        if (string.IsNullOrEmpty(connectionString))
            throw new Exception("Connection string is missing");

        if (string.IsNullOrEmpty(dbName))
            throw new Exception("Database name is missing");

        var client = new MongoClient(settings["ConnectionString"]);
        _db = client.GetDatabase(settings["DatabaseName"]);

        EnsureIndexes();
    }

    public IMongoCollection<RoadmapDocument> Roadmaps =>
        _db.GetCollection<RoadmapDocument>("roadmaps");

    public IMongoCollection<RoleProfileDocument> RoleProfiles =>
        _db.GetCollection<RoleProfileDocument>("roleProfiles");
    
    public IMongoCollection<AnalysisDocument> Analyses =>
        _db.GetCollection<AnalysisDocument>("analyses");

    private void EnsureIndexes()
    {
        // roadmaps: query by userId + isActive frequently
        var roadmapIndexes = Roadmaps.Indexes;
        roadmapIndexes.CreateOne(new CreateIndexModel<RoadmapDocument>(
            Builders<RoadmapDocument>.IndexKeys
                .Ascending(r => r.UserId)
                .Ascending(r => r.IsActive)));

        roadmapIndexes.CreateOne(new CreateIndexModel<RoadmapDocument>(
            Builders<RoadmapDocument>.IndexKeys
                .Ascending(r => r.AnalysisId)));

        // roleProfiles: query by roleKey (already _id, but add displayName index)
        RoleProfiles.Indexes.CreateOne(new CreateIndexModel<RoleProfileDocument>(
            Builders<RoleProfileDocument>.IndexKeys
                .Ascending(r => r.DisplayName)));
        
        Analyses.Indexes.CreateOne(new CreateIndexModel<AnalysisDocument>(
            Builders<AnalysisDocument>.IndexKeys
                .Ascending(a => a.UserId)
                .Descending(a => a.CreatedAt)));

        Analyses.Indexes.CreateOne(new CreateIndexModel<AnalysisDocument>(
            Builders<AnalysisDocument>.IndexKeys
                .Ascending(a => a.UserId)
                .Ascending(a => a.AnalysisType)));
    }
}