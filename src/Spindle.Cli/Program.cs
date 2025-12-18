using Spindle;

var settings = Config.GetSettings<GeneratorSettings>("Generator");

var generator = new Generator(settings);

// Contract

generator.GeneratePolicies();
generator.GenerateQueries();
generator.GenerateCommands();
// generator.GenerateClients();

// Service

generator.GenerateTableDbContext();
generator.GenerateReadmes(settings.OutputEntityFramework6);
generator.GenerateEntities(settings.OutputEntityFramework6);
generator.GenerateReaders();
generator.GenerateWriters();
generator.GenerateAdapters();
generator.GenerateServices();

// Api

generator.GenerateControllers();