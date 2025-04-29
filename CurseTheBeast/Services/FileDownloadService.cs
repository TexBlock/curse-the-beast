using CurseTheBeast.Download;
using CurseTheBeast.Storage;
using CurseTheBeast.Utils;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace CurseTheBeast.Services;


public static class FileDownloadService
{
    static readonly int MinSizeOfShowedFile = 0 * 1024;

    public static async Task DownloadAsync(string hint, IReadOnlyCollection<FileEntry> files, bool checkCache, CancellationToken ct = default)
    {
        if (files.Count == 0)
            return;

        if (checkCache && Focused.Status("检查下载缓存", ctx => files.All(file => file.Validate())))
            return;

        var progress = AnsiConsole.Progress();
        progress.RefreshRate = TimeSpan.FromMilliseconds(200);
        progress.AutoRefresh = true;
        progress.HideCompleted = true;
        progress.AutoClear = true;
        progress.Columns([
            new MySpinnerColumn(),
            new MyTaskDescriptionColumn(20),
            new MyProgressColumn(),
            new MyProgressBarColumn(),
            new MyRemainTimeColumn()
        ]);

        await progress.StartAsync(async ctx =>
        {
            var generalTask = ctx.AddTask(Focused.Text(hint), true, files.Count);
            generalTask.State.Update<ProgressType>("type", k => ProgressType.General);
            var fileProgressDict = new Dictionary<FileEntry, ProgressTask>();

            using var queue = new DownloadQueue();
            queue.TaskStarted += task =>
            {
                lock (fileProgressDict)
                {
                    var fileProgress = fileProgressDict[task] = ctx.AddTask(Markup.Escape(task.DisplayName!.Replace(' ', '-')), true, task.Size ?? 1);
                    fileProgress.State.Update<ProgressType>("type", k => ProgressType.SingleFile);
                }
            };
            queue.TaskProgressed += (task, e) =>
            {
                lock (fileProgressDict)
                {
                    var progress = fileProgressDict[task];
                    if (e.Total == null && progress.MaxValue == 1)
                        return;

                    if (e.Total != null && progress.MaxValue != e.Total)
                        progress.MaxValue = e.Total!.Value;

                    progress.Value = e.Received;
                }
            };
            queue.TaskFinished += task =>
            {
                lock (fileProgressDict)
                {
                    if(task.Size >= MinSizeOfShowedFile && fileProgressDict.Remove(task, out var bar))
                        bar.Value = bar.MaxValue;

                    generalTask.Increment(1);
                }
            };

            await queue.DownloadAsync(files);
        });
    }

    class MySpinnerColumn : ProgressColumn
    {
        protected override bool NoWrap => true;
        readonly SpinnerColumn _baseSpinnerColumn;

        public MySpinnerColumn()
        {
            _baseSpinnerColumn = new SpinnerColumn()
            {
                Style = Focused
            };
        }

        public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
        {
            var type = task.State.Get<ProgressType>("type");
            if (type == ProgressType.General)
                return _baseSpinnerColumn.Render(options, task, deltaTime);
            else if (type == ProgressType.SingleFile)
                return Text.Empty;
            else
                throw new NotImplementedException();
        }

        public override int? GetColumnWidth(RenderOptions options)
        {
            return _baseSpinnerColumn.GetColumnWidth(options);
        }
    }

    class MyTaskDescriptionColumn : ProgressColumn
    {
        protected override bool NoWrap => true;
        readonly int _width;

        public MyTaskDescriptionColumn(int width)
        {
            _width = width;
        }

        public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
        {
            return new MyMarkup(task.Description ?? string.Empty, null, _width);
        }

        public override int? GetColumnWidth(RenderOptions options)
        {
            return _width;
        }

        class MyMarkup : IRenderable
        {
            readonly IRenderable _innerMarkup;
            readonly int _width;

            public MyMarkup(string markup, Style? style, int width)
            {
                _innerMarkup = new Markup(markup, style)
                    .Overflow(Overflow.Ellipsis)
                    .Justify(Justify.Left);
                _width = width;
            }

            public Measurement Measure(RenderOptions options, int maxWidth)
            {
                return new Measurement(Math.Min(maxWidth, _width), Math.Min(maxWidth, _width));
            }

            public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
            {
                return SplitOverflow(_innerMarkup.Render(options, int.MaxValue).First(), Math.Min(maxWidth, _width));
            }

            public static List<Segment> SplitOverflow(Segment segment, int maxWidth)
            {
                if (segment == null)
                {
                    throw new ArgumentNullException("segment");
                }
                if (segment.CellCount() <= maxWidth)
                {
                    return new List<Segment>(1)
                    {
                        segment
                    };
                }
                List<Segment> result = new List<Segment>();

                if (Math.Max(0, maxWidth - 1) == 0)
                {
                    result.Add(new Segment("~", segment.Style));
                }
                else
                {
                    result.Add(new Segment(segment.Text.Substring(0, maxWidth - 1) + "~", segment.Style));
                }
                return result;

            }
        }
    }

    class MyProgressColumn : ProgressColumn
    {
        protected override bool NoWrap => true;

        public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
        {
            var type = task.State.Get<ProgressType>("type");
            if (type == ProgressType.General)
                return new Text($"{task.Value:0}/{task.MaxValue:0}", Focused) 
                {
                    Justification = Justify.Center,
                    Overflow = Overflow.Ellipsis,
                };
            else if (type == ProgressType.SingleFile)
                return new Text($"{DataSizeUtils.Humanize(task.Value, task.MaxValue)}/{DataSizeUtils.Humanize(task.MaxValue, task.MaxValue)}", Normal)
                {
                    Justification = Justify.Center,
                    Overflow = Overflow.Ellipsis
                };
            else
                throw new NotImplementedException();
        }

        public override int? GetColumnWidth(RenderOptions options)
        {
            return 13;
        }
    }

    class MyProgressBarColumn : ProgressColumn
    {
        protected override bool NoWrap => true;
        readonly ProgressBarColumn _baseColumn = new ProgressBarColumn();

        public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
        {
            _baseColumn.CompletedStyle = getStyle(task);
            _baseColumn.RemainingStyle = Low;
            return _baseColumn.Render(options, task, deltaTime);
        }

        public override int? GetColumnWidth(RenderOptions options)
        {
            return _baseColumn.GetColumnWidth(options);
        }
    }

    class MyRemainTimeColumn : ProgressColumn
    {
        protected override bool NoWrap => true;
        readonly RemainingTimeColumn _baseColumn = new RemainingTimeColumn();

        public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
        {
            _baseColumn.Style = getStyle(task);
            return _baseColumn.Render(options, task, deltaTime);
        }

        public override int? GetColumnWidth(RenderOptions options)
        {
            return _baseColumn.GetColumnWidth(options);
        }
    }

    static Style getStyle(ProgressTask task)
    {
        var type = task.State.Get<ProgressType>("type");
        if (type == ProgressType.General)
            return Focused;
        else if (type == ProgressType.SingleFile)
            return Normal;
        else
            throw new NotImplementedException();
    }

    enum ProgressType : int
    {
        None,
        General,
        SingleFile
    }
}
