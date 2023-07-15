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
internal static class Ratio
{
    public static List<int> Distribute(int total, IList<int> ratios, IList<int>? minimums = null)
    {
        if (minimums != null)
        {
            ratios = ratios.Zip(minimums, (a, b) => (ratio: a, min: b)).Select(a => a.min > 0 ? a.ratio : 0).ToList();
        }

        var totalRatio = ratios.Sum();

        var totalRemaining = total;
        var distributedTotal = new List<int>();

        minimums ??= ratios.Select(_ => 0).ToList();

        foreach (var (ratio, minimum) in ratios.Zip(minimums, (a, b) => (a, b)))
        {
            var distributed = (totalRatio > 0)
                ? Math.Max(minimum, (int)Math.Ceiling(ratio * totalRemaining / (double)totalRatio))
                : totalRemaining;

            distributedTotal.Add(distributed);
            totalRatio -= ratio;
            totalRemaining -= distributed;
        }

        return distributedTotal;
    }
}