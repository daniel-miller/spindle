using System.Data;
using System.Text;

namespace Spindle;

public class Generator
{
    private string _input;
    private string _output;
    private GeneratorSettings _settings;

    private IDatabase _database { get; set; }
    private EntityList _entities { get; set; }
    private List<Entity> _tables { get; set; }

    public Generator(GeneratorSettings settings)
    {
        _settings = settings;

        _input = settings.TemplateFolder;

        _output = settings.OutputFolder;

        _database = settings.DatabaseType == "SqlServer"
            ? new SqlServerDatabase(settings)
            : new PostgresDatabase(settings);

        _entities = _database.GetEntities();

        _tables = _entities
            .Where(x => x.StorageStructure == "Table" || x.StorageStructure == "Projection")
            .ToList();
    }

    public void GenerateCommands()
    {
        GenerateCommands(_database.GetEntities("Table"));
        GenerateCommands(_database.GetEntities("Projection"));
    }

    public void GenerateCommands(EntityList tables)
    {
        var components = tables.GetComponents();

        foreach (var component in components)
        {
            var features = tables.GetSubcomponents(component);

            foreach (var subcomponent in features)
            {
                var entities = tables.GetEntities(component, subcomponent);

                foreach (var entity in entities)
                {
                    var commands = new[] { "Create", "Modify", "Delete" };

                    foreach (var command in commands)
                    {
                        var template = GetTemplate("Commands-" + command);

                        var e = tables.GetEntity(component, subcomponent, entity);
                        var createProperties = PropertyDeclarations(e, PropertyType.All);
                        var deleteProperties = PropertyDeclarations(e, PropertyType.OnlyPrimaryKey);
                        var modifyProperties = PropertyDeclarations(e, PropertyType.All);

                        var code = template
                            .Replace("$Namespace", GetNamespace(_settings.PlatformName, "Contract"))
                            .Replace("$EntityNamePlural", Inflector.Pluralize(entity))
                            .Replace("$EntityName", entity)
                            .Replace("$CreateProperties", createProperties)
                            .Replace("$ModifyProperties", modifyProperties)
                            .Replace("$DeleteProperties", deleteProperties)
                            ;

                        var folder = GeneratePath(_output, "Contract", component, subcomponent, entity, "Commands");

                        var file = Path.Combine(folder, command + entity + ".cs");

                        File.WriteAllText(file, code);
                    }
                }
            }
        }
    }

    public void GenerateQueries()
    {
        GenerateQueries(_database.GetEntities("Table"));
        GenerateQueries(_database.GetEntities("Projection"));
    }

    public void GenerateQueries(EntityList tables)
    {
        var components = tables.GetComponents();

        foreach (var component in components)
        {
            var subcomponents = tables.GetSubcomponents(component);

            foreach (var subcomponent in subcomponents)
            {
                var entities = tables.GetEntities(component, subcomponent);

                foreach (var entity in entities)
                {
                    var itemQueries = new[] { "Assert", "Retrieve" };
                    var listQueries = new[] { "Collect", "Count", "Search" };
                    var baseObjects = new[] { "Criteria", "Match", "Model" };

                    var queries = itemQueries
                        .Concat(listQueries)
                        .Concat(baseObjects);

                    foreach (var query in queries)
                    {
                        var template = GetTemplate("Queries-" + query);

                        var e = tables.GetEntity(component, subcomponent, entity);

                        var single = PropertyDeclarations(e, PropertyType.OnlyPrimaryKey);
                        var multipleForInterface = PropertyDeclarations(e, PropertyType.ExcludePrimaryKey, true, false, 2, true);
                        var multiple = PropertyDeclarations(e, PropertyType.ExcludePrimaryKey, true);
                        var match = PropertyDeclarations(e, PropertyType.OnlyPrimaryKey);
                        var model = PropertyDeclarations(e, PropertyType.All);

                        var code = template
                            .Replace("$Namespace", GetNamespace(_settings.PlatformName, "Contract"))
                            .Replace("$EntityNamePlural", Inflector.Pluralize(entity))
                            .Replace("$EntityName", entity)
                            .Replace("$SingleItemProperties", single)
                            .Replace("$MultipleItemPropertiesForInterface", multipleForInterface)
                            .Replace("$MultipleItemProperties", multiple)
                            .Replace("$MatchItemProperties", match)
                            .Replace("$ModelItemProperties", model)
                            ;

                        var folder = GeneratePath(_output, "Contract", component, subcomponent, entity, "Queries");

                        var filename = query + entity;

                        if (listQueries.Contains(query))
                            filename = query + Inflector.Pluralize(entity);

                        if (baseObjects.Contains(query))
                            filename = entity + query;

                        if (query == "Criteria")
                            filename = "I" + filename;

                        var file = Path.Combine(folder, filename + ".cs");

                        File.WriteAllText(file, code);
                    }
                }
            }
        }
    }

    public void GenerateReaders()
    {
        var output = Path.Combine(_output, "Service");

        foreach (var entity in _entities)
        {
            var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;
            var folder = GeneratePath(output, entity.ComponentName, entity.ComponentPart, "Data", storageName);
            GenerateReader(entity, folder);
        }
    }

    public void GenerateReader(Entity entity, string output)
    {
        var template = GetTemplate("EntityReader");

        var text = template;

        var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;
        var storageStructure = entity.StorageStructure;
        if (storageStructure == "Projection")
            storageStructure = "Table";

        var primaryKey = StorageKey(entity);

        var parameters = PrimaryKeyMethodParameters("Service", entity, true);
        var columns = PrimaryKeyColumnNames(entity);
        var expression = PrimaryKeyEqualityExpression(entity, true);
        var query = BuildQuery(entity);

        var assignEntityToMatch = new StringBuilder();

        for (int i = 0; i < primaryKey.Length; i++)
        {
            var columnName = ConvertSnakeCaseToPascalCase(primaryKey[i]);

            assignEntityToMatch.Append("                " + columnName + " = entity." + columnName);

            if (i < primaryKey.Length - 1)
                assignEntityToMatch.Append(",");

            assignEntityToMatch.AppendLine("");
        }

        text = text.Replace("$AssignEntityToMatch", assignEntityToMatch.ToString());
        text = text.Replace("$ContractNamespace", GetNamespace(_settings.PlatformName, "Contract"));
        text = text.Replace("$ServiceNamespace", GetNamespace(_settings.PlatformName, "Service", entity.ComponentName));
        text = text.Replace("$StorageStructure", storageStructure);
        text = text.Replace("$StorageName", storageName);
        text = text.Replace("$EntityName", entity.EntityName);
        text = text.Replace("$PrimaryKeyEqualityExpression", expression);
        text = text.Replace("$PrimaryKeyColumnNames", columns);
        text = text.Replace("$PrimaryKeyMethodParameters", parameters);
        text = text.Replace("$Query", query);

        var path = Path.Combine(output, entity.EntityName + "Reader.cs");

        File.WriteAllText(path, text);
    }

    public void GenerateWriters()
    {
        var output = Path.Combine(_output, "Service");

        foreach (var entity in _entities)
        {
            var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;
            var folder = GeneratePath(output, entity.ComponentName, entity.ComponentPart, "Data", storageName);
            GenerateWriter(entity, folder);
        }
    }

    public void GenerateWriter(Entity entity, string output)
    {
        var template = GetTemplate("EntityWriter");

        var text = template;

        var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;
        var storageStructure = entity.StorageStructure;
        if (storageStructure == "Projection")
            storageStructure = "Table";

        var parameters = PrimaryKeyMethodParameters("Service", entity, true);
        var expression = PrimaryKeyEqualityExpression(entity, true);
        var properties = PrimaryKeyPropertyNames(entity);

        text = text.Replace("$ContractNamespace", GetNamespace(_settings.PlatformName, "Contract"));
        text = text.Replace("$ServiceNamespace", GetNamespace(_settings.PlatformName, "Service", entity.ComponentName));
        text = text.Replace("$StorageStructure", storageStructure);
        text = text.Replace("$StorageName", storageName);
        text = text.Replace("$EntityName", entity.EntityName);
        text = text.Replace("$PrimaryKeyEqualityExpression", expression);
        text = text.Replace("$PrimaryKeyMethodParameters", parameters);
        text = text.Replace("$PrimaryKeyPropertyNames", properties);

        var path = Path.Combine(output, entity.EntityName + "Writer.cs");

        File.WriteAllText(path, text);
    }

    public void GenerateServices()
    {
        var output = Path.Combine(_output, "Service");

        foreach (var entity in _entities)
        {
            var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;
            var folder = GeneratePath(output, entity.ComponentName, entity.ComponentPart, "Data", storageName);
            GenerateService(entity, folder);
        }
    }

    public void GenerateService(Entity entity, string output)
    {
        var template = GetTemplate("EntityService");

        var text = template;

        var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;

        var parameters = PrimaryKeyMethodParameters("Service", entity, true);
        var expression = PrimaryKeyEqualityExpression(entity, true);
        var properties = PrimaryKeyPropertyNames(entity);
        var arguments = PrimaryKeyMethodArguments(entity, true);
        var argumentsForModify = PrimaryKeyMethodArguments(entity, false, "modify");

        text = text.Replace("$ContractNamespace", GetNamespace(_settings.PlatformName, "Contract"));
        text = text.Replace("$ServiceNamespace", GetNamespace(_settings.PlatformName, "Service", entity.ComponentName));
        text = text.Replace("$StorageStructure", entity.StorageStructure);
        text = text.Replace("$StorageName", storageName);
        text = text.Replace("$EntityName", entity.EntityName);
        text = text.Replace("$PrimaryKeyEqualityExpression", expression);
        text = text.Replace("$PrimaryKeyMethodParameters", parameters);
        text = text.Replace("$PrimaryKeyPropertyNames", properties);
        text = text.Replace("$PrimaryKeyMethodArgumentsForModify", argumentsForModify);
        text = text.Replace("$PrimaryKeyMethodArguments", arguments);

        var path = Path.Combine(output, entity.EntityName + "Service.cs");

        File.WriteAllText(path, text);
    }

    public void GenerateAdapters()
    {
        var output = Path.Combine(_output, "Service");

        foreach (var entity in _entities)
        {
            var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;
            var folder = GeneratePath(output, entity.ComponentName, entity.ComponentPart, "Data", storageName);
            GenerateAdapter(entity, folder);
        }
    }

    public void GenerateAdapter(Entity entity, string output)
    {
        var template = GetTemplate("EntityAdapter");

        var text = template;

        var columns = _database.GetTable(entity.StorageSchema, entity.StorageTable).Columns;

        var primaryKey = StorageKey(entity);

        var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;

        var parameters = PrimaryKeyMethodParameters("Service", entity, true);
        var expression = PrimaryKeyEqualityExpression(entity, true);
        var properties = PrimaryKeyPropertyNames(entity);
        var arguments = PrimaryKeyMethodArguments(entity, true);
        var argumentsForModify = PrimaryKeyMethodArguments(entity, false, "modify");

        var assignModifyToEntity = new StringBuilder();

        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];

            if (primaryKey.Contains(column.ColumnName))
                continue;

            var columnName = ConvertSnakeCaseToPascalCase(column.ColumnName);

            assignModifyToEntity.AppendLine("        entity." + columnName + " = modify." + columnName + ";");
        }

        var assignCreateToEntity = new StringBuilder();

        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];

            var columnName = ConvertSnakeCaseToPascalCase(column.ColumnName);

            assignCreateToEntity.Append("            " + columnName + " = create." + columnName);

            if (i < columns.Count - 1)
                assignCreateToEntity.AppendLine(",");
        }

        var assignEntityToModel = new StringBuilder();

        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];

            var columnName = ConvertSnakeCaseToPascalCase(column.ColumnName);

            assignEntityToModel.Append("            " + columnName + " = entity." + columnName);

            if (i < columns.Count - 1)
                assignEntityToModel.AppendLine(",");
        }

        var assignEntityToMatch = new StringBuilder();

        for (int i = 0; i < primaryKey.Length; i++)
        {
            var columnName = ConvertSnakeCaseToPascalCase(primaryKey[i]);

            assignEntityToMatch.Append("            " + columnName + " = entity." + columnName);

            if (i < primaryKey.Length - 1)
                assignEntityToMatch.Append(",");

            assignEntityToMatch.AppendLine("");
        }

        text = text.Replace("$ContractNamespace", GetNamespace(_settings.PlatformName, "Contract"));
        text = text.Replace("$ServiceNamespace", GetNamespace(_settings.PlatformName, "Service", entity.ComponentName));
        text = text.Replace("$StorageStructure", entity.StorageStructure);
        text = text.Replace("$StorageName", storageName);
        text = text.Replace("$EntityName", entity.EntityName);
        text = text.Replace("$PrimaryKeyEqualityExpression", expression);
        text = text.Replace("$PrimaryKeyMethodParameters", parameters);
        text = text.Replace("$PrimaryKeyPropertyNames", properties);
        text = text.Replace("$PrimaryKeyMethodArgumentsForModify", argumentsForModify);
        text = text.Replace("$PrimaryKeyMethodArguments", arguments);

        text = text.Replace("$AssignModifyToEntity", assignModifyToEntity.ToString());
        text = text.Replace("$AssignCreateToEntity", assignCreateToEntity.ToString());
        text = text.Replace("$AssignEntityToModel", assignEntityToModel.ToString());
        text = text.Replace("$AssignEntityToMatch", assignEntityToMatch.ToString());

        var path = Path.Combine(output, entity.EntityName + "Adapter.cs");

        File.WriteAllText(path, text);
    }

    public void GenerateControllers()
    {
        var output = Path.Combine(_output, "Api");

        foreach (var entity in _entities)
        {
            var folder = GeneratePath(output, entity.ComponentName, entity.ComponentPart);
            GenerateController(entity, folder);
        }
    }

    public void GenerateController(Entity entity, string output)
    {
        var template = entity.StorageStructure == "Projection"
            ? GetTemplate("ControllerForProjection")
            : GetTemplate("Controller");

        var text = template;

        const string package = "Api";

        var parameters = PrimaryKeyMethodParameters(package, entity, true);
        var arguments = PrimaryKeyMethodArguments(entity, true);
        var pkValuesForCreate = PrimaryKeyPropertyValuesForCreate(entity, "create");
        var pkValuesForCreateRetrieve = PrimaryKeyPropertyValuesForGet(entity, "create");
        var pkValuesForModify = PrimaryKeyPropertyValuesForCreate(entity, "modify");
        var pkValuesForModifyRetrieve = PrimaryKeyPropertyValuesForGet(entity, "modify");

        var swaggerHeading = SwaggerHeading(entity);
        var entityPolicy = "Policies." + entity.GetNamespace();
        var variable = EntityVariable(entity);

        text = text
            .Replace("$ApiNamespace", GetNamespace(_settings.PlatformName, "Api"))
            .Replace("$ComponentName", entity.ComponentName)
            .Replace("$ContractNamespace", GetNamespace(_settings.PlatformName, "Contract"))
            .Replace("$ServiceNamespace", GetNamespace(_settings.PlatformName, "Service", entity.ComponentName))
            .Replace("$EntityNamePluralVariable", Inflector.Pluralize(variable))
            .Replace("$EntityNamePlural", Inflector.Pluralize(entity.EntityName))
            .Replace("$EntityNameVariable", variable)
            .Replace("$EntityName", entity.EntityName)
            .Replace("$EntityLabelPlural", Inflector.Pluralize(Inflector.ToSentenceCase(entity.EntityName)))
            .Replace("$EntityLabel", Inflector.ToSentenceCase(entity.EntityName))
            .Replace("$EntityPolicy", entityPolicy)
            .Replace("$SwaggerHeading", swaggerHeading)
            .Replace("$CollectionPath", entity.GetCollectionPath())
            .Replace("$CollectionKey", entity.CollectionKey)
            .Replace("$PrimaryKeyMethodArguments", arguments)
            .Replace("$PrimaryKeyMethodParameters", parameters)
            .Replace("$PrimaryKeyValuesForCreateRetrieve", pkValuesForCreateRetrieve)
            .Replace("$PrimaryKeyValuesForCreate", pkValuesForCreate)
            .Replace("$PrimaryKeyValuesForModifyRetrieve", pkValuesForModifyRetrieve)
            .Replace("$PrimaryKeyValuesForModify", pkValuesForModify)
            ;

        var path = Path.Combine(output, entity.EntityName + "Controller.cs");

        File.WriteAllText(path, text);
    }

    public void GenerateClients(bool generateUnitTests = false)
    {
        foreach (var entity in _entities)
        {
            var output = Path.Combine(_output, "Contract");
            var folder = GeneratePath(output, entity.ComponentName, entity.ComponentPart, entity.EntityName);
            GenerateClient(entity, folder);

            if (generateUnitTests)
            {
                output = Path.Combine(_output, "Contract.Test");
                folder = GeneratePath(output, entity.ComponentName, entity.ComponentPart);
                GenerateClientTest(entity, folder);
            }
        }
    }

    public void GenerateClient(Entity entity, string output)
    {
        var template = entity.StorageStructure == "Projection"
            ? GetTemplate("ClientForProjection")
            : GetTemplate("Client");

        var text = template;

        const string package = "Contract";

        var declarations = PropertyDeclarations(entity, PropertyType.All, false, true, 1);

        var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;

        var entities = Inflector.Pluralize(entity.EntityName);

        var parameters = PrimaryKeyMethodParameters(package, entity, true);
        var arguments = PrimaryKeyMethodArguments(entity, true);
        var expression = PrimaryKeyEqualityExpression(entity);
        var values = PrimaryKeyPropertyValues(entity);
        var pkValuesForDelete = PrimaryKeyPropertyValuesForGet(entity, "delete");
        var pkValuesForModify = PrimaryKeyPropertyValuesForGet(entity, "modify");

        var swaggerHeading = SwaggerHeading(entity);
        var pkParameters = PrimaryKeyMethodParameters(package, entity, true);
        var pkArguments = PrimaryKeyMethodArguments(entity, true);
        var assertArguments = PrimaryKeyMethodArguments(entity, false, "assert");
        var deleteArguments = PrimaryKeyMethodArguments(entity, false, "delete");
        var modifyArguments = PrimaryKeyMethodArguments(entity, false, "modify");

        var apiRoute = "Endpoints." + entity.GetNamespace();

        text = text
            .Replace("$SwaggerHeading", swaggerHeading)
            .Replace("$ApiNamespace", GetNamespace(_settings.PlatformName, "Api"))
            .Replace("$ApiRoute", apiRoute)
            .Replace("$ContractNamespace", GetNamespace(_settings.PlatformName, "Contract"))
            .Replace("$EntityNamePlural", Inflector.Pluralize(entity.EntityName))
            .Replace("$EntityName", entity.EntityName)
            .Replace("$PrimaryKeyMethodArguments", arguments)
            .Replace("$PrimaryKeyMethodParameters", parameters)
            .Replace("$PrimaryKeyValuesForDelete", pkValuesForDelete)
            .Replace("$PrimaryKeyValuesForModify", pkValuesForModify)
            ;

        var path = Path.Combine(output, entity.EntityName + "Client.cs");

        File.WriteAllText(path, text);
    }

    public void GenerateClientTest(Entity entity, string output)
    {
        var template = GetTemplate("ClientTest");

        var text = template;

        const string package = "Contract";

        var declarations = PropertyDeclarations(entity, PropertyType.All, false, true, 1);

        var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;

        var entities = Inflector.Pluralize(entity.EntityName);

        var parameters = PrimaryKeyMethodParameters(package, entity, true);
        var arguments = PrimaryKeyMethodArguments(entity, true);
        var expression = PrimaryKeyEqualityExpression(entity);
        var values = PrimaryKeyPropertyValues(entity);
        var pkValuesForDelete = PrimaryKeyPropertyValuesForGet(entity, "delete");
        var pkValuesForModify = PrimaryKeyPropertyValuesForGet(entity, "modify");

        var swaggerHeading = SwaggerHeading(entity);
        var pkParameters = PrimaryKeyMethodParameters(package, entity, true);
        var pkArguments = PrimaryKeyMethodArguments(entity, true);
        var assertArguments = PrimaryKeyMethodArguments(entity, false, "assert");
        var deleteArguments = PrimaryKeyMethodArguments(entity, false, "delete");
        var modifyArguments = PrimaryKeyMethodArguments(entity, false, "modify");

        var apiRoute = "Endpoints." + entity.GetNamespace();

        text = text
            .Replace("$ApiNamespace", GetNamespace(_settings.PlatformName, "Api"))
            .Replace("$CommonNamespace", GetNamespace(_settings.PlatformName, "Common"))
            .Replace("$ContractNamespace", GetNamespace(_settings.PlatformName, "Contract"))
            .Replace("$EntityNamePlural", Inflector.Pluralize(entity.EntityName))
            .Replace("$EntityName", entity.EntityName)
            .Replace("$ApiRoute", apiRoute)
            .Replace("$SwaggerHeading", swaggerHeading)
            .Replace("$PrimaryKeyMethodArguments", arguments)
            .Replace("$PrimaryKeyMethodParameters", parameters)
            .Replace("$PrimaryKeyValuesForDelete", pkValuesForDelete)
            .Replace("$PrimaryKeyValuesForModify", pkValuesForModify)
            ;

        var path = Path.Combine(output, entity.EntityName + "Client.Tests.cs");

        File.WriteAllText(path, text);
    }

    public void GenerateEntities(bool outputEntityFramework6)
    {
        foreach (var entity in _entities)
        {
            {
                var output = Path.Combine(_output, "Service");
                var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;
                var folder = GeneratePath(output, entity.ComponentName, entity.ComponentPart, "Data", storageName);
                GenerateEntity(entity, folder);
                GenerateEntityConfiguration(entity, folder);
            }

            if (outputEntityFramework6)
            {
                var output = Path.Combine(_output, "Service.EF6");
                var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;
                var folder = GeneratePath(output, entity.ComponentName, entity.ComponentPart, "Data", storageName);
                GenerateEntity6(entity, folder);
                GenerateEntity6Configuration(entity, folder);
            }
        }
    }

    public void GenerateEntity(Entity entity, string output)
    {
        var template = GetTemplate("Entity");

        var text = template;

        var declarations = PropertyDeclarations(entity, PropertyType.All, false, true, 1);

        var className = entity.EntityName + "Entity";

        text = text.Replace("$Namespace", GetNamespace(_settings.PlatformName, "Service", entity.ComponentName));
        text = text.Replace("$ClassName", className);
        text = text.Replace("$ClassProperties", declarations);

        var path = Path.Combine(output, className + ".cs");

        File.WriteAllText(path, text);
    }

    public void GenerateEntityConfiguration(Entity entity, string output)
    {
        var template = GetTemplate("EntityConfiguration");

        var text = template;

        var keys = entity.StorageKey.Split([',']).ToArray();
        for (var i = 0; i < keys.Length; i++)
            keys[i] = ConvertSnakeCaseToPascalCase(keys[i]);
        var pkColumnNames = "x." + string.Join(", x.", keys);

        text = text.Replace("$Namespace", GetNamespace(_settings.PlatformName, "Service", entity.ComponentName));
        text = text.Replace("$EntityName", entity.EntityName);
        text = text.Replace("$StorageSchema", entity.StorageSchema);
        text = text.Replace("$StorageTable", entity.StorageTable);
        text = text.Replace("$PrimaryKeyColumnNames", pkColumnNames);
        text = text.Replace("$ColumnSpecifications", GetColumnSpecifications(entity));

        var path = Path.Combine(output, entity.EntityName + "Configuration.cs");

        File.WriteAllText(path, text);
    }

    public void GenerateValidators()
    {
        var output = Path.Combine(_output, "Service");

        foreach (var entity in _entities)
        {
            var layers = new[] { "Data", "State", "Process", "UI" };

            foreach (var layer in layers)
            {
                var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;
                var folder = GeneratePath(output, entity.ComponentName, entity.ComponentPart, "Data", storageName);
                GenerateValidator(entity, folder);
            }
        }
    }

    public void GenerateValidator(Entity entity, string output)
    {
        var template = GetTemplate("Validator");

        var text = template;

        var declarations = PropertyDeclarations(entity, PropertyType.All, false, true, 1);

        var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;

        text = text.Replace("$Namespace", GetNamespace(_settings.PlatformName, "Service", entity.ComponentName));
        text = text.Replace("$ContractNamespace", GetNamespace(_settings.PlatformName, "Contract"));
        text = text.Replace("$StorageName", storageName);
        text = text.Replace("$StorageName", storageName);
        text = text.Replace("$EntityName", entity.EntityName);

        var path = Path.Combine(output, storageName + "Validator.cs");

        File.WriteAllText(path, text);
    }

    public void GenerateReadmes(bool outputEntityFramework6)
    {
        string[] strings = ["Service"];

        var services = outputEntityFramework6
            ? ["Service", "Service.EF6"]
            : strings;

        foreach (var service in services)
        {
            var output = Path.Combine(_output, service);

            foreach (var entity in _entities)
            {
                var layers = new[] { "Data", "State", "Process", "UI" };

                foreach (var layer in layers)
                {
                    var folder = GeneratePath(output, entity.ComponentName, entity.ComponentPart, layer);

                    GenerateReadme(entity, layer, folder);
                }
            }
        }
    }

    public void GeneratePolicies()
    {
        var text = new StringBuilder();

        var tables = _database.GetEntities();

        var components = tables.GetComponents();

        foreach (var component in components)
        {
            text.AppendLine($"        public static partial class {component} // Component");
            text.AppendLine($"        {{");

            var features = tables.GetSubcomponents(component);

            foreach (var feature in features)
            {
                text.AppendLine($"            public static partial class {feature} // Subcomponent");
                text.Append($"            {{");

                var entities = tables.GetEntities(component, feature);

                foreach (var entity in entities)
                {
                    var endpoint = tables.GetEntity(component, feature, entity);
                    var subtype = endpoint.StorageStructure;
                    var isProjection = subtype == "Projection";
                    var isTable = subtype == "Table";

                    if (!(isProjection || isTable))
                        continue;

                    var collection = endpoint.GetCollectionPath();

                    if (endpoint.ComponentType == "Plugin")
                    {
                        var a = endpoint.ComponentName.ToLower();
                        var b = endpoint.ComponentPart.ToLower();
                        var c = endpoint.CollectionSlug;

                        if (c.StartsWith($"{b}-"))
                            c = c.Substring(b.Length + 1);

                        // collection = $"{a}/{b}/{c}";
                    }

                    var blockForTable = $@"
                public static partial class {entity} // Entity
                {{
                    // Queries

                    public const string Assert = ""{collection}/assert"";
                    public const string Retrieve = ""{collection}/retrieve"";
                    
                    public const string Collect = ""{collection}/collect"";
                    public const string Count = ""{collection}/count"";
                    public const string Search = ""{collection}/search"";
                    public const string Download = ""{collection}/download"";

                    // Commands

                    public const string Create = ""{collection}/create"";
                    public const string Delete = ""{collection}/delete"";
                    public const string Modify = ""{collection}/modify"";
                }}";

                    var blockForProjection = $@"
                public static partial class {entity} // Entity
                {{
                    // Queries

                    public const string Assert = ""{collection}/assert"";
                    public const string Retrieve = ""{collection}/retrieve"";
                    
                    public const string Collect = ""{collection}/collect"";
                    public const string Count = ""{collection}/count"";
                    public const string Search = ""{collection}/search"";
                    public const string Download = ""{collection}/download"";
                }}";

                    if (isProjection)
                        text.AppendLine(blockForProjection);
                    else if (isTable)
                        text.AppendLine(blockForTable);
                }

                text.AppendLine("            }");
                if (feature != features.Last())
                    text.AppendLine();
            }

            text.AppendLine("        }");
            if (component != components.Last())
                text.AppendLine();
        }

        var template = GetTemplate("Policies");

        var code = template
            .Replace("$Namespace", GetNamespace(_settings.PlatformName, "Contract"))
            .Replace("$Policies", text.ToString())
            ;

        var output = Path.Combine(_output, "Contract");

        CreateFolder(output);

        var path = Path.Combine(output, "Policies.cs");

        File.WriteAllText(path, code);
    }

    public void GenerateReadme(Entity entity, string layer, string output)
    {
        var template = GetTemplate("Readme");

        var text = template;

        text = text.Replace("$ComponentPart", entity.ComponentPart);
        text = text.Replace("$ComponentLayer", layer);
        text = text.Replace("$ComponentName", entity.ComponentName);
        text = text.Replace("$ComponentType", entity.ComponentType.ToLower());

        text = text.Replace("$EntitySummary", GetEntitySummary(entity, layer));

        var path = Path.Combine(output, "README.md");

        File.WriteAllText(path, text);
    }

    public void GenerateTableDbContext()
    {
        var template = GetTemplate("TableDbContext");

        var text = template;

        text = text.Replace("$Usings", GetServiceUsings());
        text = text.Replace("$Namespace", _settings.PlatformName + ".Service");
        text = text.Replace("$DbSetProperties", GetDbSetProperties());
        text = text.Replace("$DbSetConfigurations", GetDbSetConfigurations());

        var output = Path.Combine(_output, "Service", "Metadata");

        CreateFolder(output);

        var path = Path.Combine(output, "TableDbContext.cs");

        File.WriteAllText(path, text);
    }

    private string GetDbSetProperties()
    {
        var text = new StringBuilder();

        var tableGroups = _tables
            .OrderBy(x => x.ComponentType + ": " + x.ComponentName)
            .GroupBy(x => x.ComponentType + ": " + x.ComponentName);

        foreach (var tableGroup in tableGroups)
        {
            var lines = new List<string>();

            foreach (var entity in tableGroup)
            {
                var dbSetName = entity.EntityName;

                var className = dbSetName + "Entity";

                lines.Add("    internal DbSet<" + className + "> " + dbSetName + " { get; set; }");
            }

            lines.Sort();

            text.AppendLine("    // " + tableGroup.Key);

            foreach (var line in lines)
                text.AppendLine(line);

            text.AppendLine();
        }

        return text.ToString();
    }

    private string GetDbSetConfigurations()
    {
        var text = new StringBuilder();

        var tableGroups = _tables
            .OrderBy(x => x.ComponentType + ": " + x.ComponentName)
            .GroupBy(x => x.ComponentType + ": " + x.ComponentName);

        foreach (var tableGroup in tableGroups)
        {
            var lines = new List<string>();

            foreach (var entity in tableGroup)
            {
                var storageName = entity.EntityName;

                lines.Add("        builder.ApplyConfiguration(new " + storageName + "Configuration());");
            }

            lines.Sort();

            text.AppendLine("        // " + tableGroup.Key);

            foreach (var line in lines)
                text.AppendLine(line);

            text.AppendLine();
        }

        return text.ToString();
    }

    private string GetServiceUsings()
    {
        var text = new StringBuilder();

        var components = _tables.Select(x => x.ComponentName).Distinct().OrderBy(x => x);

        foreach (var component in components)
        {
            text.AppendLine("using " + _settings.PlatformName + ".Service." + component + ";");
        }

        return text.ToString();
    }

    private string GetEntitySummary(Entity entity, string layer)
    {
        var futureSchemaChanges = _entities.GetFutureSchemaChanges(entity.ComponentName, entity.ComponentPart);

        var text = new StringBuilder();

        var component = entity.ComponentName;

        var part = entity.ComponentPart;

        if (layer == "Data")
        {
            text.AppendLine($"The **Data** folder contains code for - including entities, entity type configurations, entity readers and readers, entity adapters, and entity services. This is the **persistence** (or entity) layer for {component} {part}.");

            if (futureSchemaChanges != null)
            {
                text.AppendLine("");
                text.AppendLine("## Proposed Improvements");
                text.AppendLine("");
                text.AppendLine("When time and opportunity permit, the following database schema changes should be considered, to improve alignment with current naming conventions:");
                text.AppendLine("");
                foreach (var item in futureSchemaChanges)
                    text.AppendLine(item);
            }
        }

        if (layer == "State")
            text.AppendLine($"The **State** folder contains code for models - including aggregates and changes. This is the **domain** layer for {component} {part}.");

        if (layer == "Process")
            text.AppendLine($"The **Process** folder contains application logic and business rules - including commands, command generators, command handlers, change handlers (projectors and processors), queries, and query handlers. This is the **application** layer for {part}.");

        if (layer == "UI")
            text.AppendLine($"The **UI** folder contains code for presentation logic. This is the code *behind* the **user interface** layer for {component} {part}.");

        return text.ToString();
    }

    private string GetColumnSpecifications(Entity entity, bool isCore = true)
    {
        var text = new StringBuilder();

        var columns = _database.GetTable(entity.StorageSchema, entity.StorageTable).Columns;

        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];

            text.Append("        ");

            if (isCore)
                text.Append("builder.");

            text.Append("Property(x => x." + ConvertSnakeCaseToPascalCase(column.ColumnName) + ")");

            text.Append(".HasColumnName(\"" + column.ColumnName + "\")");

            if (!column.AllowDBNull)
            {
                text.Append(".IsRequired()");
            }

            if (column.DataType == typeof(string))
            {
                if (_database.GetNativeType(entity.StorageTable, column.ColumnName) == "nvarchar")
                {
                    text.Append(".IsUnicode(true)");
                }
                else
                {
                    text.Append(".IsUnicode(false)");
                }

                if (-1 < column.MaxLength && column.MaxLength < int.MaxValue)
                {
                    text.Append(".HasMaxLength(" + column.MaxLength + ")");
                }
            }
            else if (column.DataType == typeof(decimal))
            {
                var precision = _database.GetColumnPrecision(entity.StorageTable, column.ColumnName);
                var scale = _database.GetColumnScale(entity.StorageTable, column.ColumnName);
                if (precision != null && scale != null)
                {
                    text.Append($".HasPrecision({precision}, {scale})");
                }
            }

            text.AppendLine(";");
        }

        return text.ToString();
    }

    public string GetNamespace(string a, string? b = null, string? c = null)
    {
        var space = a;

        if (b != null)
            space += "." + b;

        if (c != null)
            space += "." + c;

        return space;
    }

    private string GetTemplate(string name)
    {
        var path = Path.Combine(_input, name + ".txt");
        var text = File.ReadAllText(path);
        return text;
    }

    public string CreateFolder(string folder)
    {
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
        return folder;
    }

    public string GeneratePath(string a, string b, string c = "-", string d = "-", string e = "-", string f = "-")
    {
        var path = a;

        if (b != "-")
            path += $@"\{b}";

        if (c != "-")
            path += $@"\{c}";

        if (d != "-")
            path += $@"\{d}";

        if (e != "-")
            path += $@"\{e}";

        if (f != "-")
            path += $@"\{f}";

        return CreateFolder(path);
    }

    private string ConvertSnakeCaseToPascalCase(string input)
    {
        // If there are no underscores then assume the input is already in PascalCase.
        if (!input.Contains('_'))
            return input;

        var words = input.Split('_');

        var pascalCase = string.Join(string.Empty, words.Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower()));

        if (pascalCase == "event")
            pascalCase = "@event";

        return pascalCase;
    }

    #region EF6

    public void GenerateEntity6(Entity entity, string output)
    {
        var template = GetTemplate("Entity6");

        var text = template;

        var declarations = PropertyDeclarations(entity, PropertyType.All, false, false, 2);

        var className = entity.GetStorageStructurePrefix() + entity.EntityName + "Entity";

        text = text.Replace("$Namespace", GetNamespace(_settings.PlatformName, "Service", entity.ComponentName));
        text = text.Replace("$ClassName", className);
        text = text.Replace("$ClassProperties", declarations);

        var path = Path.Combine(output, className + ".cs");

        File.WriteAllText(path, text);
    }

    public void GenerateEntity6Configuration(Entity entity, string output)
    {
        var template = GetTemplate("Entity6Configuration");

        var text = template;

        var keys = entity.StorageKey.Split([',']).ToArray();
        for (var i = 0; i < keys.Length; i++)
            keys[i] = ConvertSnakeCaseToPascalCase(keys[i]);
        var pkColumnNames = "x." + string.Join(", x.", keys);

        var storageName = entity.GetStorageStructurePrefix() + entity.EntityName;

        text = text.Replace("$Namespace", GetNamespace(_settings.PlatformName, "Service", entity.ComponentName));
        text = text.Replace("$StorageName", storageName);

        text = text.Replace("$StorageSchema", entity.StorageSchema);
        text = text.Replace("$StorageTable", entity.StorageTable);
        text = text.Replace("$PrimaryKeyColumnNames", pkColumnNames);

        text = text.Replace("$ColumnSpecifications", GetColumnSpecifications(entity, false));

        var path = Path.Combine(output, storageName + "Configuration.cs");

        File.WriteAllText(path, text);
    }

    #endregion

    public string SwaggerHeading(Entity entity)
    {
        var heading = entity.ComponentName;

        if (entity.ComponentPart != "-")
            heading += " API: " + entity.ComponentPart;

        return $"\"{heading}\"";
    }

    public string EntityVariable(Entity entity)
    {
        return Inflector.Decapitalize(entity.EntityName);
    }

    public string[] StorageKey(Entity entity)
    {
        return entity.StorageKey.Split(new char[] { ',' });
    }

    public string BuildQuery(Entity entity)
    {
        var table = _database.GetTable(entity.StorageSchema, entity.StorageTable);

        var query = string.Empty;

        for (int i = 0; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];
            var columnName = ConvertSnakeCaseToPascalCase(column.ColumnName);
            var typeName = EntityColumnComparer.GetTypeAlias(column.DataType);

            query += "\r\n";
            query += "        // if (criteria." + columnName + " != null)\r\n";
            query += "        //    q = q.Where(x => x." + columnName + " == criteria." + columnName + ");\r\n";
        }

        return query;
    }

    public string PrimaryKeyEqualityExpression(Entity entity, bool stripIdentifierSuffix = false)
    {
        var pkEqualityExpression = string.Empty;

        var table = _database.GetTable(entity.StorageSchema, entity.StorageTable);

        var primaryKey = entity.StorageKey.Split(',');

        for (int i = 0; i < primaryKey.Length; i++)
        {
            var column = table.Columns[primaryKey[i]];

            if (column == null)
                throw new Exception($"Column not found: {primaryKey[i]}");

            var typeName = EntityColumnComparer.GetTypeAlias(column.DataType);
            var variableName = Inflector.Decapitalize(ConvertSnakeCaseToPascalCase(column.ColumnName));

            if (stripIdentifierSuffix)
            {
                if (variableName.EndsWith("Id"))
                    variableName = variableName.Substring(0, variableName.Length - 2);

                if (variableName.EndsWith("Identifier"))
                    variableName = variableName.Substring(0, variableName.Length - 10);
            }

            if (variableName == "event")
                variableName = "@event";

            if (i > 0)
            {
                pkEqualityExpression += " && ";
            }

            pkEqualityExpression += "x." + ConvertSnakeCaseToPascalCase(column.ColumnName) + " == " + variableName;
        }

        return pkEqualityExpression;
    }

    public string PrimaryKeyColumnNames(Entity entity)
    {
        var table = _database.GetTable(entity.StorageSchema, entity.StorageTable);
        var primaryKey = entity.StorageKey.Split(',');

        var pkColumnNames = "";

        for (int i = 0; i < primaryKey.Length; i++)
        {
            var column = table.Columns[primaryKey[i]];

            if (column == null)
                throw new Exception($"Column not found: {primaryKey[i]}");

            if (i > 0)
            {
                pkColumnNames += ", ";
            }

            pkColumnNames += column.ColumnName;
        }

        return pkColumnNames;
    }

    public string PrimaryKeyMethodArguments(Entity entity, bool stripIdentifierSuffix = false, string? argument = null)
    {
        var table = _database.GetTable(entity.StorageSchema, entity.StorageTable);
        var primaryKey = entity.StorageKey.Split(',');

        var pkMethodArguments = "";

        for (int i = 0; i < primaryKey.Length; i++)
        {
            var column = table.Columns[primaryKey[i]];

            if (column == null)
                throw new Exception($"Column not found: {primaryKey[i]}");

            var columnName = ConvertSnakeCaseToPascalCase(column.ColumnName);
            var typeName = EntityColumnComparer.GetTypeAlias(column.DataType);
            var variableName = argument == null
                ? Inflector.Decapitalize(ConvertSnakeCaseToPascalCase(column.ColumnName))
                : ConvertSnakeCaseToTitleCase(columnName)
                ;

            if (stripIdentifierSuffix)
            {
                if (variableName.EndsWith("Id"))
                    variableName = variableName.Substring(0, variableName.Length - 2);

                if (variableName.EndsWith("Identifier"))
                    variableName = variableName.Substring(0, variableName.Length - 10);
            }

            if (variableName == "event")
                variableName = "@event";

            if (i > 0)
            {
                pkMethodArguments += ", ";
            }

            if (argument != null)
            {
                pkMethodArguments += argument + "." + variableName;
            }
            else
            {
                variableName = Inflector.Decapitalize(variableName);

                if (variableName == "event")
                    variableName = "@event";

                pkMethodArguments += variableName;
            }

        }

        return pkMethodArguments;
    }

    public string PrimaryKeyMethodParameters(string package, Entity entity, bool stripIdentifierSuffix = false)
    {
        var table = _database.GetTable(entity.StorageSchema, entity.StorageTable);
        var primaryKey = entity.StorageKey.Split(',');

        var pkMethodParameters = "";

        for (int i = 0; i < primaryKey.Length; i++)
        {
            var column = table.Columns[primaryKey[i]];

            if (column == null)
                throw new Exception($"Column not found: {primaryKey[i]}");

            var typeName = EntityColumnComparer.GetTypeAlias(column.DataType);
            var variableName = Inflector.Decapitalize(ConvertSnakeCaseToPascalCase(column.ColumnName));

            if (stripIdentifierSuffix)
            {
                if (variableName.EndsWith("Id"))
                    variableName = variableName.Substring(0, variableName.Length - 2);

                if (variableName.EndsWith("Identifier"))
                    variableName = variableName.Substring(0, variableName.Length - 10);
            }

            if (variableName == "event")
                variableName = "@event";

            if (i > 0)
            {
                pkMethodParameters += ", ";
            }

            pkMethodParameters += (package == "Api" ? "[FromRoute] " : "") + typeName + " " + variableName;
        }

        return pkMethodParameters;
    }

    public string PrimaryKeyPropertyNames(Entity entity)
    {
        var pkPropertyNames = string.Empty;

        var table = _database.GetTable(entity.StorageSchema, entity.StorageTable);

        var primaryKey = entity.StorageKey.Split(',');

        for (int i = 0; i < primaryKey.Length; i++)
        {
            var column = table.Columns[primaryKey[i]];

            if (column == null)
                throw new Exception($"Column not found: {primaryKey[i]}");

            var typeName = EntityColumnComparer.GetTypeAlias(column.DataType);
            var variableName = Inflector.Decapitalize(ConvertSnakeCaseToPascalCase(column.ColumnName));

            if (variableName == "event")
                variableName = "@event";

            if (i > 0)
                pkPropertyNames += ", ";

            pkPropertyNames += "entity." + ConvertSnakeCaseToPascalCase(column.ColumnName);
        }

        return pkPropertyNames;
    }

    public string PrimaryKeyPropertyValues(Entity entity)
    {
        var pkPropertyValues = string.Empty;

        var table = _database.GetTable(entity.StorageSchema, entity.StorageTable);

        var primaryKey = entity.StorageKey.Split(',');

        for (int i = 0; i < primaryKey.Length; i++)
        {
            var column = table.Columns[primaryKey[i]];

            if (column == null)
                throw new Exception($"Column not found: {primaryKey[i]}");

            var typeName = EntityColumnComparer.GetTypeAlias(column.DataType);
            var variableName = Inflector.Decapitalize(ConvertSnakeCaseToPascalCase(column.ColumnName));

            if (variableName == "event")
                variableName = "@event";

            pkPropertyValues += column.ColumnName + " {entity." + column.ColumnName + "}";
        }

        return pkPropertyValues;
    }

    public string PrimaryKeyPropertyValuesForCreate(Entity entity, string objectName)
    {
        var pkPropertyValuesForCreate = "";

        var table = _database.GetTable(entity.StorageSchema, entity.StorageTable);
        var primaryKey = entity.StorageKey.Split(',');

        for (int i = 0; i < primaryKey.Length; i++)
        {
            var column = table.Columns[primaryKey[i]];

            if (column == null)
                throw new Exception($"Column not found: {primaryKey[i]}");

            var columnName = ConvertSnakeCaseToPascalCase(column.ColumnName);
            var typeName = EntityColumnComparer.GetTypeAlias(column.DataType);
            var variableName = Inflector.Decapitalize(columnName);

            if (variableName == "event")
                variableName = "@event";

            pkPropertyValuesForCreate += columnName + " {" + objectName + "." + columnName + "}";
        }

        return pkPropertyValuesForCreate;
    }

    public string PrimaryKeyPropertyValuesForGet(Entity entity, string objectName)
    {
        var table = _database.GetTable(entity.StorageSchema, entity.StorageTable);

        var primaryKey = entity.StorageKey.Split(',');

        var pkPropertyValuesForGet = "";

        for (int i = 0; i < primaryKey.Length; i++)
        {
            var column = table.Columns[primaryKey[i]];

            if (column == null)
                throw new Exception($"Column not found: {primaryKey[i]}");

            var columnName = ConvertSnakeCaseToPascalCase(column.ColumnName);

            if (i > 0)
                pkPropertyValuesForGet += ", ";

            pkPropertyValuesForGet += objectName + "." + columnName;
        }

        return pkPropertyValuesForGet;
    }

    public string PropertyDeclarations(Entity entity, PropertyType type, bool allowNull = false, bool isCore = false, int tabs = 2, bool removeModifier = false)
    {
        var indent = new string(' ', tabs * 4);

        var table = _database.GetTable(entity.StorageSchema, entity.StorageTable);
        var primaryKey = entity.StorageKey.Split(',');

        var columns = new List<DataColumn>();
        foreach (DataColumn column in table.Columns)
        {
            if (type == PropertyType.OnlyPrimaryKey && !primaryKey.Contains(column.ColumnName))
                continue;

            if (type == PropertyType.ExcludePrimaryKey && primaryKey.Contains(column.ColumnName))
                continue;

            columns.Add(column);
        }
        columns.Sort(new EntityColumnComparer());

        var declarations = "";
        string? lasttype = null;

        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];

            var typeName = EntityColumnComparer.GetTypeAlias(column.DataType);
            var datatype = typeName;

            var isString = column.DataType == typeof(string);
            var isByteArray = column.DataType == typeof(byte[]);

            var isRequired = !allowNull && !column.AllowDBNull;

            if (!isRequired && (isCore || !(isString || isByteArray)))
                datatype += "?";

            if (declarations != string.Empty && lasttype != typeName)
                declarations += "\r\n";

            declarations += indent + (!removeModifier ? "public " : "") + datatype + " " + ConvertSnakeCaseToPascalCase(column.ColumnName) + " { get; set; }";

            if (isCore && (isString || isByteArray) && !datatype.EndsWith("?"))
                declarations += " = null!;";

            if (i < columns.Count - 1)
                declarations += "\r\n";

            lasttype = typeName;
        }

        return declarations;
    }

    private string ConvertSnakeCaseToTitleCase(string input)
    {
        // If there are no underscores then assume the input is already in the desired case.
        if (!input.Contains('_'))
            return input;

        var words = input.Split('_');

        var titleCase = string.Join(string.Empty, words.Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower()));

        if (titleCase == "Event")
            titleCase = "@Event";

        return titleCase;
    }
}
