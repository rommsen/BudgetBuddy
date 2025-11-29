# Comdirect to YNAB Importer

This application allows you to import transactions from your Comdirect account into YNAB (You Need A Budget).

## Features

*   Fetches recent transactions from your Comdirect account.
*   Uploads new transactions to a specified YNAB budget and account.
*   Prevents duplicate transaction imports by checking existing transaction references.
*   Supports rule-based category mapping for automatic categorization of transactions.

## Rule-Based Category Mapping

This application supports rule-based category mapping to automatically assign YNAB categories to imported transactions based on patterns found in the transaction's memo field.

### Configuration File (`rules.yml`)

You can define your categorization rules in a YAML file named `rules.yml`. By default, the application looks for this file in:
-   Linux/macOS: `$XDG_CONFIG_HOME/comdirect2ynab/rules.yml` (or `~/.config/comdirect2ynab/rules.yml` if `$XDG_CONFIG_HOME` is not set)
-   Windows: `%APPDATA%\comdirect2ynabules.yml`

You can override the path to this file using the `--rules` command-line option (see below).

**Schema:**

The `rules.yml` file has the following structure:

```yaml
# List of rules, processed from top to bottom. The first match wins.
rules:
  # Match using simple text (case-insensitive)
  - match: "REWE"
    category: "Lebensmittel" # Groceries
  - match: "EDEKA"
    category: "Lebensmittel"
  - match: "ALDI"
    category: "Lebensmittel"

  # Match using a more specific pattern
  - match: "DB Fernverkehr"
    category: "Reisen · Bahn" # Travel · Rail

  # Match using Regular Expressions (case-insensitive)
  # This rule matches any memo containing "Amazon"
  - match: ".*Amazon.*"
    category: "Shopping"

  # Example: Match a specific streaming service
  - match: "NETFLIX"
    category: "Entertainment · Streaming"

  # Example: Match based on a common part of a utility bill
  - match: "Stadtwerke Musterstadt"
    category: "Wohnen · Nebenkosten" # Housing · Utilities
```

**Details:**

*   `rules`: A list of rule objects.
    *   `match`: A string pattern to match against the transaction's memo. This can be a simple string (case-insensitive containment check) or a valid .NET Regular Expression (also applied case-insensitively).
    *   `category`: The human-readable name of the YNAB category to assign if the `match` pattern is found in the memo. The name must correspond to an existing category in your YNAB budget. The matching is case-insensitive, and common umlaut variations (e.g., "ä" vs "ae") are handled.

The application validates this schema at startup. If the file is malformed or a category name cannot be resolved to a YNAB category ID, the program will print an error and exit.

### Command-Line Option: `--rules`

You can specify a custom path to your rules file using the `--rules` command-line option:

```bash
dotnet run -- --transfer --rules /path/to/your/custom-rules.yml
```

If this option is not provided, the default path (mentioned above) will be used.

## Setup and Configuration

*(TODO: Add details on how to configure appsettings.json, YNAB API key, Comdirect credentials handling, etc.)*

## Usage

*(TODO: Add details on command-line arguments like --transfer, --ynab-infos, etc.)*

```bash
# Example: Transfer transactions
dotnet run -- --transfer

# Example: Get YNAB budget and account information
dotnet run -- --ynab-infos
```

## Building from Source

1.  Clone the repository.
2.  Ensure you have the .NET SDK installed (version specified in `global.json` or the project file, e.g., .NET 9).
3.  Navigate to the project directory (`Comdirect_to_Ynab`).
4.  Run `dotnet build`.

## Running Tests

1.  Navigate to the test project directory (`Comdirect2YNAB.Tests`).
2.  Run `dotnet test`.

## Contributing

*(TODO: Add contribution guidelines if applicable)*
