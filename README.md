# Spindle

A robust C# code generation tool that helps to create application layers from database metadata. This generator creates contracts, services, API controllers, and SDK clients for PostgreSQL and SQL Server databases.

## Features

- **Multi-Database Support**: Works with both PostgreSQL and SQL Server
- **Complete Layer Generation**: Creates contracts, services, API controllers, and SDK client code
- **Entity Framework Support**: Generates entities and configurations for EF Core (and optionally EF6)
- **Flexible Configuration**: JSON-based configuration with environment-specific overrides
- **Consistent Naming**: Smart pluralization and case conversion utilities
- **Template-Based**: Uses customizable text templates for code generation

## Architecture

The generator follows a layered architecture pattern:

- **Contract Layer**: Commands, queries, models, and policies
- **Service Layer**: Data access, entities, adapters, and business services
- **API Layer**: REST controllers with proper routing and validation
- **SDK Layer**: Strongly-typed HTTP clients for API consumption

## Getting Started

### Prerequisites

- .NET 6.0 or later
- Access to a PostgreSQL or SQL Server database
- Database with metadata tables configured

### Configuration

Create an `appsettings.json` file. For example:

```json
{
  "Generator": {
    "PlatformName": "YourProject",
    "TemplateFolder": "./Template",
    "OutputFolder": "../../dist/output",
    "DatabaseType": "Postgres",
    "DatabaseConnection": "Host=localhost; Database=demo; Username=postgres; Password=PASSWORD;"
  }
}
```

For local development, create `appsettings.local.json`:

```json
{
  "Generator": {
    "DatabaseType": "SqlServer",
    "DatabaseConnection": "Data Source=(local); Initial Catalog=LocalDB; User Id=sa; Password=LocalPassword; Encrypt=false;"
  }
}
```

### Usage

1. **Set up your database metadata**: Ensure your database contains the required metadata table with entity definitions
2. **Configure the generator**: Update configuration files with your database connection and output preferences
3. **Run the generator**:
   ```bash
   dotnet run
   ```

The generator creates the following basic structure:

```
output/
├── Contract/
│   ├── Commands/
│   └── Queries/  
├── Service/
│   └── Data/
└── Api/
    └── Controllers/
```

## Core Components

### ConfigurationManager
Singleton configuration manager with support for:
- Multiple configuration sources
- Environment-specific settings
- Automatic reloading
- Strongly-typed settings binding

### Entity System
- **Entity**: Represents database entities with metadata
- **EntityList**: Collection with specialized query methods
- **EntityColumnComparer**: Ensures consistent property ordering

### Database Abstraction
- **IDatabase**: Common interface for database operations
- **PostgresDatabase**: PostgreSQL-specific implementation
- **SqlServerDatabase**: SQL Server-specific implementation

### Code Generation
- **Generator**: Main orchestrator for all code generation
- **Template System**: Text-based templates for customizable output
- **Inflector**: String manipulation utilities (pluralization, casing)

## Generated Code Examples

### Commands
```csharp
public class CreateUser
{
    public Guid UserId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}
```

### Queries
```csharp
public class RetrieveUser
{
    public Guid UserId { get; set; }
}

public class UserModel
{
    public Guid UserId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}
```

### API Controllers
```csharp
[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUser create) { }
    
    [HttpGet("{userId}")]
    public async Task<UserModel> Retrieve([FromRoute] Guid userId) { }
}
```

## Database Schema Requirements

The generator expects metadata tables in your database:

### PostgreSQL
```sql
-- metadata.t_entity table with columns:
-- component_type, component_name, component_part, entity_name
-- collection_slug, collection_key
-- storage_structure, storage_schema, storage_table, storage_key
```

### SQL Server
```sql
-- metadata.TEntity table with columns:
-- ComponentType, ComponentName, ComponentPart, EntityName
-- CollectionSlug, CollectionKey  
-- StorageStructure, StorageSchema, StorageTable, StorageKey
```

## Customization

### Templates
Place custom templates in the configured `TemplateFolder`. Templates use simple token replacement:

```
namespace $Namespace;

public class $EntityName
{
$ClassProperties
}
```

### Entity Framework Support
- Enable EF6 support with `OutputEntityFramework6: true`
- Generates both EF Core and EF6 entities when enabled
- Includes proper type configurations and DbContext setup

## Advanced Features

### Smart Naming
- Automatic pluralization of entity names
- Consistent case conversion (PascalCase, camelCase, snake_case)
- Handles irregular plurals and special cases

### Namespace Generation
- Hierarchical namespace structure based on components
- Supports plugin architecture with nested namespaces
- Automatic policy and endpoint generation

### Validation & Error Handling
- Comprehensive input validation
- Detailed error messages for missing entities
- Graceful handling of database connection issues

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For questions or issues:
1. Check the documentation
2. Review existing issues
3. Create a new issue with detailed information about your problem
