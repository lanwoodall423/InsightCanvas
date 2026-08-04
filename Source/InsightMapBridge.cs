using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace InsightCanvas
{
    /// <summary>Temporary map target information attached to an entity or event.</summary>
    public sealed class InsightMapReference
    {
        private readonly List<IntVec3> cells;

        internal InsightMapReference(string id, object target, Map map, IntVec3 cell, IEnumerable<IntVec3> cells, int? worldTile)
        {
            Id = id ?? string.Empty;
            Target = target;
            Map = map;
            Cell = cell;
            this.cells = new List<IntVec3>();
            if (cells != null)
                foreach (IntVec3 value in cells)
                    if (value.IsValid) this.cells.Add(value);
            WorldTile = worldTile;
        }

        public string Id { get; private set; }
        public object Target { get; private set; }
        public Map Map { get; private set; }
        public IntVec3 Cell { get; private set; }
        public int? WorldTile { get; private set; }
        public IReadOnlyList<IntVec3> Cells => cells;
        internal List<IntVec3> CellsInternal => cells;
        public bool HasLocation => Cell.IsValid || cells.Count > 0 || WorldTile.HasValue;
    }

    /// <summary>
    /// Creates safe standard actions for map and world references. Overlay requests are temporary and owned by the
    /// source window, so a closed view cannot leave permanent map drawing behind.
    /// </summary>
    public static class InsightMapBridge
    {
        private static readonly Dictionary<string, InsightMapReference> links = new Dictionary<string, InsightMapReference>(StringComparer.Ordinal);
        private static readonly object unownedOwnerToken = new object();
        private static object activeOwnerToken;

        internal static object CurrentOwnerToken => activeOwnerToken ?? unownedOwnerToken;

        internal static IDisposable BeginOwner(object ownerToken)
        {
            object previous = activeOwnerToken;
            activeOwnerToken = ownerToken ?? unownedOwnerToken;
            return new OwnerScope(previous);
        }

        /// <summary>Registers a transient id that can be used by an InsightEvent.MapLinkId.</summary>
        public static void RegisterLink(string id, InsightMapReference reference)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (reference == null) links.Remove(id);
            else links[id] = reference;
        }

        /// <summary>Resolves a previously registered event map link.</summary>
        public static InsightMapReference ResolveLink(string id)
        {
            InsightMapReference reference;
            return id != null && links.TryGetValue(id, out reference) ? reference : null;
        }

        /// <summary>Removes a transient event map link.</summary>
        public static bool UnregisterLink(string id) => id != null && links.Remove(id);

        public static InsightMapReference For(Thing thing)
        {
            return thing == null ? null : new InsightMapReference("thing:" + thing.thingIDNumber, thing,
                thing.Spawned ? thing.Map : null, thing.Spawned ? thing.Position : IntVec3.Invalid, null, null);
        }

        public static InsightMapReference For(Pawn pawn) => For((Thing)pawn);

        public static InsightMapReference ForCell(Map map, IntVec3 cell)
        {
            return new InsightMapReference("cell:" + cell.x + ":" + cell.z, null, map, cell, null, null);
        }

        public static InsightMapReference ForCells(Map map, IEnumerable<IntVec3> cells, string id = null)
        {
            return new InsightMapReference(id ?? "cells", null, map, IntVec3.Invalid, cells, null);
        }

        public static InsightMapReference For(Zone zone)
        {
            return zone == null ? null : FromObject("zone", zone);
        }

        public static InsightMapReference For(Area area)
        {
            return area == null ? null : FromObject("area", area);
        }

        public static InsightMapReference For(WorldObject worldObject)
        {
            return worldObject == null ? null : new InsightMapReference("world:" + worldObject.ID, worldObject, null,
                IntVec3.Invalid, null, (int)worldObject.Tile);
        }

        public static InsightMapReference ForWorldTile(int tile) =>
            new InsightMapReference("tile:" + tile, null, null, IntVec3.Invalid, null, tile);

        /// <summary>Creates a focus-and-select action for a reference.</summary>
        public static InsightAction Focus(string id, InsightMapReference reference, bool select = true)
        {
            return new InsightAction(id ?? "focus", "InsightCanvas_Focus".Translate(), () => FocusNow(reference, select),
                reference != null, "InsightCanvas_FocusTip".Translate(), !(InsightCanvasMod.Settings?.PreserveWindowOnMapAction ?? false));
        }

        /// <summary>Creates a selection-only action for a live Thing or world object.</summary>
        public static InsightAction Select(string id, InsightMapReference reference)
        {
            return new InsightAction(id ?? "select", "InsightCanvas_Select".Translate(), () => SelectNow(reference),
                reference != null, null, false);
        }

        /// <summary>Creates a temporary cell flash/radius/outline action.</summary>
        public static InsightAction Flash(string id, InsightMapReference reference, float seconds = 3f)
        {
            return new InsightAction(id ?? "flash", "InsightCanvas_PreviewMap".Translate(),
                () => InsightMapOverlay.Register(reference, InsightOverlayKind.Flash, seconds, CurrentOwnerToken),
                reference != null && reference.HasLocation);
        }

        /// <summary>Registers a temporary heatmap-like field overlay.</summary>
        public static InsightAction Heatmap(string id, InsightMapReference reference, float seconds = 3f)
        {
            return new InsightAction(id ?? "heatmap", "InsightCanvas_ShowArea".Translate(),
                () => InsightMapOverlay.Register(reference, InsightOverlayKind.Heatmap, seconds, CurrentOwnerToken),
                reference != null && reference.HasLocation);
        }

        /// <summary>Registers a temporary field outline.</summary>
        public static InsightAction Outline(string id, InsightMapReference reference, float seconds = 3f)
        {
            return new InsightAction(id ?? "outline", "InsightCanvas_ShowArea".Translate(),
                () => InsightMapOverlay.Register(reference, InsightOverlayKind.Outline, seconds, CurrentOwnerToken),
                reference != null && reference.HasLocation);
        }

        /// <summary>Registers a temporary radius ring at a reference cell.</summary>
        public static InsightAction Radius(string id, InsightMapReference reference, float seconds = 3f)
        {
            return new InsightAction(id ?? "radius", "InsightCanvas_ShowRadius".Translate(),
                () => InsightMapOverlay.Register(reference, InsightOverlayKind.Radius, seconds, CurrentOwnerToken),
                reference != null && reference.Cell.IsValid);
        }

        /// <summary>Registers a temporary line through the reference cell sequence.</summary>
        public static InsightAction Path(string id, InsightMapReference reference, float seconds = 3f)
        {
            return new InsightAction(id ?? "path", "InsightCanvas_ShowPath".Translate(),
                () => InsightMapOverlay.Register(reference, InsightOverlayKind.Path, seconds, CurrentOwnerToken),
                reference != null && reference.Cells.Count > 1);
        }

        public static void Clear(InsightWindow owner = null) => InsightMapOverlay.Clear(owner);

        internal static void ClearOwnerToken(object ownerToken) => InsightMapOverlay.ClearOwner(ownerToken);

        private static void FocusNow(InsightMapReference reference, bool select)
        {
            if (reference == null) return;
            try
            {
                Thing thing = reference.Target as Thing;
                if (thing != null && thing.Spawned)
                {
                    if (select) Find.Selector.Select(thing);
                    CameraJumper.TryJump(thing);
                    return;
                }
                WorldObject worldObject = reference.Target as WorldObject;
                if (worldObject != null)
                {
                    GlobalTargetInfo target = new GlobalTargetInfo(worldObject);
                    if (select) CameraJumper.TryJumpAndSelect(target); else CameraJumper.TryJump(target);
                    return;
                }
                if (reference.Cell.IsValid && reference.Map != null)
                {
                    CameraJumper.TryJump(reference.Cell, reference.Map);
                    return;
                }
                if (reference.WorldTile.HasValue)
                {
                    CameraJumper.TryJump(new PlanetTile(reference.WorldTile.Value));
                }
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[Insight Canvas] Map focus failed: " + exception.Message, "insight-map-focus".GetHashCode());
            }
        }

        private static void SelectNow(InsightMapReference reference)
        {
            if (reference == null) return;
            Thing thing = reference.Target as Thing;
            if (thing?.Spawned == true) Find.Selector.Select(thing);
            else if (reference.Target is WorldObject worldObject) CameraJumper.TrySelect(new GlobalTargetInfo(worldObject));
        }

        private static InsightMapReference FromObject(string prefix, object target)
        {
            Map map = ReadProperty<Map>(target, "Map");
            List<IntVec3> cells = ReadCells(target);
            IntVec3 cell = ReadProperty<IntVec3>(target, "Position");
            if (!cell.IsValid && cells.Count > 0) cell = cells[0];
            return new InsightMapReference(prefix + ":" + target.GetHashCode(), target, map, cell, cells, null);
        }

        private static T ReadProperty<T>(object target, string name)
        {
            if (target == null) return default(T);
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !typeof(T).IsAssignableFrom(property.PropertyType)) return default(T);
            try { return (T)property.GetValue(target, null); } catch { return default(T); }
        }

        private static List<IntVec3> ReadCells(object target)
        {
            List<IntVec3> result = new List<IntVec3>();
            if (target == null) return result;
            PropertyInfo property = target.GetType().GetProperty("Cells", BindingFlags.Public | BindingFlags.Instance) ??
                target.GetType().GetProperty("ActiveCells", BindingFlags.Public | BindingFlags.Instance);
            if (property == null) return result;
            try
            {
                IEnumerable<IntVec3> values = property.GetValue(target, null) as IEnumerable<IntVec3>;
                if (values != null) foreach (IntVec3 value in values) if (value.IsValid) result.Add(value);
            }
            catch { result.Clear(); }
            return result;
        }

        private sealed class OwnerScope : IDisposable
        {
            private readonly object previous;
            private bool disposed;

            internal OwnerScope(object previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                activeOwnerToken = previous;
            }
        }
    }

    internal enum InsightOverlayKind
    {
        Flash,
        Heatmap,
        Outline,
        Radius,
        Path
    }

    internal sealed class InsightOverlayEntry
    {
        public InsightMapReference Reference;
        public InsightOverlayKind Kind;
        public int ExpireTick;
        public object OwnerToken;
    }

    /// <summary>Map-draw extension point used only for temporary Insight Canvas previews.</summary>
    public sealed class InsightMapOverlayComponent : MapComponent
    {
        private readonly List<InsightOverlayEntry> entries = new List<InsightOverlayEntry>();

        public InsightMapOverlayComponent(Map map) : base(map) { }

        internal void Add(InsightOverlayEntry entry) => entries.Add(entry);
        internal void Clear() => entries.Clear();
        internal void ClearOwner(object ownerToken) => InsightOverlayOwnership.ClearOwner(entries, ownerToken,
            entry => entry == null ? null : entry.OwnerToken);

        public override void ExposeData()
        {
            // Overlay state is intentionally transient and is never written to a save.
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            entries.RemoveAll(entry => entry == null || entry.Reference == null || entry.ExpireTick <= now);
        }

        public override void MapComponentDraw()
        {
            if (Find.CurrentMap != map) return;
            for (int i = 0; i < entries.Count; i++)
            {
                InsightOverlayEntry entry = entries[i];
                InsightMapReference reference = entry.Reference;
                Color color = entry.Kind == InsightOverlayKind.Heatmap ? new Color(0.2f, 0.75f, 0.8f, 0.42f) : new Color(0.92f, 0.65f, 0.2f, 0.75f);
                if (reference.Cells.Count > 0 && (entry.Kind == InsightOverlayKind.Heatmap || entry.Kind == InsightOverlayKind.Outline))
                    GenDraw.DrawFieldEdges(reference.CellsInternal, color);
                if (reference.Cell.IsValid && (entry.Kind == InsightOverlayKind.Flash || entry.Kind == InsightOverlayKind.Radius || entry.Kind == InsightOverlayKind.Heatmap))
                    GenDraw.DrawRadiusRing(reference.Cell, entry.Kind == InsightOverlayKind.Heatmap ? 2.5f : 1.1f, color);
                if (entry.Kind == InsightOverlayKind.Path)
                    for (int cell = 1; cell < reference.Cells.Count; cell++)
                        GenDraw.DrawLineBetween(reference.Cells[cell - 1].ToVector3Shifted(), reference.Cells[cell].ToVector3Shifted(), SimpleColor.Cyan);
            }
        }
    }

    internal static class InsightMapOverlay
    {
        public static void Register(InsightMapReference reference, InsightOverlayKind kind, float seconds, object ownerToken)
        {
            if (reference?.Map == null || !reference.HasLocation) return;
            InsightMapOverlayComponent component = reference.Map.GetComponent<InsightMapOverlayComponent>();
            if (component == null) return;
            component.Add(new InsightOverlayEntry
            {
                Reference = reference,
                Kind = kind,
                ExpireTick = (Find.TickManager?.TicksGame ?? 0) + Mathf.Max(1, Mathf.RoundToInt(seconds * 60f)),
                OwnerToken = ownerToken
            });
        }

        public static void Clear(InsightWindow owner)
        {
            if (Current.Game == null) return;
            if (owner == null)
            {
                ClearAll();
                return;
            }
            ClearOwner(owner.OverlayOwnerToken);
        }

        internal static void ClearOwner(object ownerToken)
        {
            if (Current.Game == null) return;
            for (int i = 0; i < Current.Game.Maps.Count; i++)
            {
                InsightMapOverlayComponent component = Current.Game.Maps[i].GetComponent<InsightMapOverlayComponent>();
                if (component == null) continue;
                component.ClearOwner(ownerToken);
            }
        }

        private static void ClearAll()
        {
            for (int i = 0; i < Current.Game.Maps.Count; i++)
            {
                InsightMapOverlayComponent component = Current.Game.Maps[i].GetComponent<InsightMapOverlayComponent>();
                if (component == null) continue;
                // Explicit global cleanup remains available for map changes and shutdown paths.
                component.Clear();
            }
        }
    }
}
