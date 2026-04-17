"""Output formatters package."""
from azure_cost_cli.formatters.base import BaseOutputFormatter
from azure_cost_cli.formatters.json_formatter import JsonOutputFormatter
from azure_cost_cli.formatters.text_formatter import TextOutputFormatter
from azure_cost_cli.formatters.markdown_formatter import MarkdownOutputFormatter
from azure_cost_cli.formatters.csv_formatter import CsvOutputFormatter
from azure_cost_cli.formatters.console_formatter import ConsoleOutputFormatter
from azure_cost_cli.models import OutputFormat


def create_formatters() -> dict[OutputFormat, BaseOutputFormatter]:
    return {
        OutputFormat.CONSOLE: ConsoleOutputFormatter(),
        OutputFormat.JSON: JsonOutputFormatter(),
        OutputFormat.JSONC: JsonOutputFormatter(colored=True),
        OutputFormat.TEXT: TextOutputFormatter(),
        OutputFormat.MARKDOWN: MarkdownOutputFormatter(),
        OutputFormat.CSV: CsvOutputFormatter(),
    }
