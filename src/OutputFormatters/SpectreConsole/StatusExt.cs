/*
Some of the SpectreConsole code is internal, so copied here for reuse.
 
The following license applies to this code:

MIT License

Copyright (c) 2020 Patrik Svensson, Phil Scott, Nils Andresen

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using Spectre.Console;

namespace AzureCostCli.OutputFormatters.SpectreConsole;

public class StatusExt
    {
        private readonly IAnsiConsole _console;

        /// <summary>
        /// Gets or sets the spinner.
        /// </summary>
        public Spinner? Spinner { get; set; }

        /// <summary>
        /// Gets or sets the spinner style.
        /// </summary>
        public Style? SpinnerStyle { get; set; } = Color.Yellow;

        /// <summary>
        /// Gets or sets a value indicating whether or not status
        /// should auto refresh. Defaults to <c>true</c>.
        /// </summary>
        public bool AutoRefresh { get; set; } = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="Status"/> class.
        /// </summary>
        /// <param name="console">The console.</param>
        public StatusExt(IAnsiConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
        }

        /// <summary>
        /// Starts a new status display.
        /// </summary>
        /// <param name="status">The status to display.</param>
        /// <param name="action">The action to execute.</param>
        public void Start(string status, Action<StatusContext> action)
        {
            var task = StartAsync(status, ctx =>
            {
                action(ctx);
                return Task.CompletedTask;
            });

            task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Starts a new status display.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="status">The status to display.</param>
        /// <param name="func">The action to execute.</param>
        /// <returns>The result.</returns>
        public T Start<T>(string status, Func<StatusContext, T> func)
        {
            var task = StartAsync(status, ctx => Task.FromResult(func(ctx)));
            return task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Starts a new status display.
        /// </summary>
        /// <param name="status">The status to display.</param>
        /// <param name="action">The action to execute.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task StartAsync(string status, Func<StatusContext, Task> action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (Console.IsOutputRedirected)
            {
                await action(new StatusContext()).ConfigureAwait(false);
                return;
            }
            
            _ = await StartAsync<object?>(status, async statusContext =>
            {
                await action(statusContext).ConfigureAwait(false);
                return default;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Starts a new status display and returns a result.
        /// </summary>
        /// <typeparam name="T">The result type of task.</typeparam>
        /// <param name="status">The status to display.</param>
        /// <param name="func">The action to execute.</param>
        /// <returns>A <see cref="Task{T}"/> representing the asynchronous operation.</returns>
        public async Task<T> StartAsync<T>(string status, Func<StatusContext, Task<T>> func)
        {
            if (func is null)
            {
                throw new ArgumentNullException(nameof(func));
            }

            // Set the progress columns
            var spinnerColumn = new SpinnerColumn(Spinner ?? Spinner.Known.Default)
            {
                Style = SpinnerStyle ?? Style.Plain,
            };

            var progress = new Progress(_console)
            {
                AutoClear = true,
                AutoRefresh = AutoRefresh,
            };

            progress.Columns(new ProgressColumn[]
            {
                spinnerColumn,
                new TaskDescriptionColumn(),
            });

          
            
            return await progress.StartAsync(async ctx =>
            {
                var statusContext = new StatusContext(ctx, ctx.AddTask(status), spinnerColumn);
                return await func(statusContext).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
    }
