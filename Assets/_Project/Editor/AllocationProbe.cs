using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;

/// <summary>
/// Aggregates the Profiler main-thread hierarchy inside PlayerLoop while a benchmark records,
/// attributing self time and self GC allocation to each marker (one entry per script callback).
/// </summary>
internal static class AllocationProbe
{
    private sealed class Entry { public double SelfMs, SelfGc; public int Calls; }

    private static readonly Dictionary<string, Entry> entries = new();
    private static readonly List<int> children = new();
    private static bool active, previousEnabled, previousEditor;
    private static int nextFrame;
    private static int frames;
    private static double playerLoopGc;

    internal static void Begin()
    {
        End();
        entries.Clear();
        worstFrame.Clear();
        frames = framesWithGc = 0;
        playerLoopGc = 0;
        worstFrameMs = maxFrameGc = 0;
        previousEnabled = ProfilerDriver.enabled;
        previousEditor = ProfilerDriver.profileEditor;
        ProfilerDriver.ClearAllFrames();
        ProfilerDriver.profileEditor = false;
        ProfilerDriver.enabled = true;
        nextFrame = -1;
        active = true;
        EditorApplication.update += Collect;
    }

    internal static void End()
    {
        if (!active) return;
        Collect();
        EditorApplication.update -= Collect;
        ProfilerDriver.enabled = previousEnabled;
        ProfilerDriver.profileEditor = previousEditor;
        active = false;
    }

    private static void Collect()
    {
        int last = ProfilerDriver.lastFrameIndex;
        if (last < 0) return;
        if (nextFrame < 0) nextFrame = ProfilerDriver.firstFrameIndex;
        nextFrame = System.Math.Max(nextFrame, ProfilerDriver.firstFrameIndex);
        // The newest frame can still be incomplete; read it on the next tick.
        for (; nextFrame < last; nextFrame++)
        {
            using var view = ProfilerDriver.GetHierarchyFrameDataView(nextFrame, 0,
                HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnDontSort, false);
            if (view == null || !view.valid) continue;
            var roots = new List<int>();
            view.GetItemChildren(view.GetRootItemID(), roots);
            foreach (int root in roots)
            {
                if (view.GetItemName(root) != "PlayerLoop") continue;
                float gc = view.GetItemColumnDataAsFloat(root, HierarchyFrameDataView.columnGcMemory);
                playerLoopGc += gc;
                maxFrameGc = System.Math.Max(maxFrameGc, gc);
                if (gc > 0) framesWithGc++;
                frameItems.Clear();
                Walk(view, root);
                float total = view.GetItemColumnDataAsFloat(root, HierarchyFrameDataView.columnTotalTime);
                if (total > worstFrameMs)
                {
                    worstFrameMs = total;
                    frameItems.Sort((a, b) => b.Value.CompareTo(a.Value));
                    worstFrame.Clear();
                    for (int i = 0; i < System.Math.Min(10, frameItems.Count); i++) worstFrame.Add(frameItems[i]);
                }
                frames++;
            }
        }
    }

    private static readonly List<KeyValuePair<string, float>> frameItems = new();
    private static readonly List<KeyValuePair<string, float>> worstFrame = new();
    private static float worstFrameMs, maxFrameGc;
    private static int framesWithGc;

    private static void Walk(HierarchyFrameDataView view, int item)
    {
        var local = new List<int>();
        view.GetItemChildren(item, local);
        string name = view.GetItemName(item);
        if (!entries.TryGetValue(name, out var entry)) entries[name] = entry = new Entry();
        float self = view.GetItemColumnDataAsFloat(item, HierarchyFrameDataView.columnSelfTime);
        entry.SelfMs += self;
        entry.Calls += (int)view.GetItemColumnDataAsFloat(item, HierarchyFrameDataView.columnCalls);
        if (self > 0.05f) frameItems.Add(new KeyValuePair<string, float>(name, self));
        foreach (int child in local)
        {
            // GC.Alloc samples are leaves: charge them to the marker that allocated.
            if (view.GetItemName(child) == "GC.Alloc")
                entry.SelfGc += view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnGcMemory);
            else Walk(view, child);
        }
    }

    internal static string Summary(int recordedFrames, int top = 18)
    {
        if (frames == 0) return "Profiler attribution: no frames\n";
        var inv = CultureInfo.InvariantCulture;
        var text = new StringBuilder();
        text.AppendLine(string.Format(inv, "Profiler attribution: frames={0} PlayerLoop GC/frame={1:F0}B framesWithGC={2} maxFrameGC={3:F0}B",
            frames, playerLoopGc / frames, framesWithGc, maxFrameGc));
        text.AppendLine(string.Format(inv, " Worst PlayerLoop frame {0:F1}ms, top self time:", worstFrameMs));
        foreach (var item in worstFrame) text.AppendLine(string.Format(inv, "  {0:F2}ms  {1}", item.Value, item.Key));
        var list = new List<KeyValuePair<string, Entry>>(entries);
        list.Sort((a, b) => b.Value.SelfGc.CompareTo(a.Value.SelfGc));
        text.AppendLine(" Top self GC (bytes/frame):");
        for (int i = 0; i < System.Math.Min(top, list.Count) && list[i].Value.SelfGc > 0; i++)
            text.AppendLine(string.Format(inv, "  {0:F0}B  {1}", list[i].Value.SelfGc / frames, list[i].Key));
        list.Sort((a, b) => b.Value.SelfMs.CompareTo(a.Value.SelfMs));
        text.AppendLine(" Top self time (ms/frame):");
        for (int i = 0; i < System.Math.Min(top, list.Count); i++)
            text.AppendLine(string.Format(inv, "  {0:F3}ms calls/frame={1:F1}  {2}", list[i].Value.SelfMs / frames,
                list[i].Value.Calls / (double)frames, list[i].Key));
        return text.ToString();
    }
}
