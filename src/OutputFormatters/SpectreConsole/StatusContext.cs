using Spectre.Console;

namespace AzureCostCli.OutputFormatters.SpectreConsole;
/*
Some of the SpectreConsole code is internal, so copied here for reuse.

The following license applies to this code:

MIT License

Copyright (c) 2020 Patrik Svensson, Phil Scott, Nils Andresen

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
public class StatusContext
{
    private readonly ProgressContext _context;
    private readonly ProgressTask? _task;
    private readonly SpinnerColumn _spinnerColumn;

    /// <summary>
    /// Gets or sets the current status.
    /// </summary>
    public string Status
    {
        get => _task?.Description;
        set => SetStatus(value);
    }

    /// <summary>
    /// Gets or sets the current spinner.
    /// </summary>
    public Spinner Spinner
    {
        get => _spinnerColumn.Spinner;
        set => SetSpinner(value);
    }

    /// <summary>
    /// Gets or sets the current spinner style.
    /// </summary>
    public Style? SpinnerStyle
    {
        get => _spinnerColumn.Style;
        set => _spinnerColumn.Style = value;
    }

    internal StatusContext()
    {
        
    }
    
    internal StatusContext(ProgressContext context, ProgressTask task, SpinnerColumn spinnerColumn)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _task = task ?? throw new ArgumentNullException(nameof(task));
        _spinnerColumn = spinnerColumn ?? throw new ArgumentNullException(nameof(spinnerColumn));
    }

    /// <summary>
    /// Refreshes the status.
    /// </summary>
    public void Refresh()
    {
        _context.Refresh();
    }

    private void SetStatus(string status)
    {
        if (status is null)
        {
            throw new ArgumentNullException(nameof(status));
        }

        if (_task is not null)
        {
            _task.Description = status;
        }
    }

    private void SetSpinner(Spinner spinner)
    {
        if (spinner is null)
        {
            throw new ArgumentNullException(nameof(spinner));
        }

        _spinnerColumn.Spinner = spinner;
    }
}