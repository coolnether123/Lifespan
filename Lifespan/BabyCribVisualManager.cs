using HarmonyLib;
using ModAPI.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Lifespan
{
    /// <summary>
    /// Replaces the visible child mesh for age 1-2 children with an animated crib sprite.
    /// </summary>
    internal class BabyCribVisualManager
    {
        internal const int CribAgeMinYears = 1;
        internal const int CribAgeMaxYears = 2;

        private const string ResourceName = "Lifespan.Assets.Crib.baby_crib_rolling.png";
        private const string OverlayName = "Lifespan_BabyCribVisual";
        private const int FrameWidth = 48;
        private const int FrameHeight = 32;
        private const int FrameCount = 4;
        private const int SheetMargin = 1;
        private const int SheetSpacing = 1;
        private const float PixelsPerUnit = 32f;
        private const float FrameDurationSeconds = 1f / 6f;

        private readonly IModLogger _log;
        private readonly AgeTracker _ageTracker;
        private readonly Dictionary<int, CribVisualContext> _activeVisuals = new Dictionary<int, CribVisualContext>();

        private LifespanConfig _config;
        private Sprite[] _frames;
        private Texture2D _sheetTexture;
        private bool _loadAttempted;

        private class CribVisualContext
        {
            public FamilyMember Member;
            public GameObject Overlay;
            public SpriteRenderer Renderer;
            public GameObject MeshRoot;
            public readonly List<Renderer> DisabledRenderers = new List<Renderer>();
            public int FrameIndex;
            public float NextFrameAt;
        }

        public BabyCribVisualManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker)
        {
            _log = ctx.Log;
            _config = config;
            _ageTracker = ageTracker;
        }

        public void RefreshSettings(LifespanConfig config)
        {
            _config = config;
        }

        public void Update()
        {
            if (_config == null || !_config.enableChildDevelopment || FamilyManager.Instance == null)
            {
                Clear();
                return;
            }

            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null)
            {
                Clear();
                return;
            }

            HashSet<int> eligibleIds = new HashSet<int>();
            foreach (FamilyMember member in members)
            {
                if (!IsCribEligible(member)) continue;

                int id = member.GetId();
                eligibleIds.Add(id);
                EnsureVisual(member, id);
            }

            List<int> staleIds = new List<int>();
            foreach (int id in _activeVisuals.Keys)
            {
                if (!eligibleIds.Contains(id))
                {
                    staleIds.Add(id);
                }
            }

            foreach (int id in staleIds)
            {
                RemoveVisual(id);
            }
        }

        public void Clear()
        {
            if (_activeVisuals.Count == 0) return;

            List<int> ids = new List<int>(_activeVisuals.Keys);
            foreach (int id in ids)
            {
                RemoveVisual(id);
            }
        }

        internal bool IsCribEligible(FamilyMember member)
        {
            if (member == null || member.isDead || member.isAway || !member.isChild) return false;
            if (_ageTracker == null) return false;

            int ageYears = _ageTracker.GetAgeYears(member);
            return IsCribAgeYears(ageYears);
        }

        internal static bool IsCribAgeYears(int ageYears)
        {
            return ageYears >= CribAgeMinYears && ageYears <= CribAgeMaxYears;
        }

        private void EnsureVisual(FamilyMember member, int id)
        {
            if (!TryLoadFrames()) return;

            CribVisualContext context;
            if (!_activeVisuals.TryGetValue(id, out context) || context == null || context.Overlay == null || context.Renderer == null)
            {
                context = CreateVisual(member);
                if (context == null) return;
                _activeVisuals[id] = context;
            }

            context.Member = member;
            SyncToCurrentMesh(context);
            HideCurrentMesh(context);
            AdvanceAnimation(context);
        }

        private CribVisualContext CreateVisual(FamilyMember member)
        {
            CharacterMesh mesh = GetCharacterMesh(member);
            Transform parent = mesh != null && mesh.transform.parent != null ? mesh.transform.parent : member.transform;

            GameObject overlay = new GameObject(OverlayName);
            overlay.hideFlags = HideFlags.HideAndDontSave;
            overlay.transform.parent = parent;

            int layer = mesh != null && mesh.gameObject != null ? mesh.gameObject.layer : member.gameObject.layer;
            overlay.layer = layer;

            SpriteRenderer renderer = overlay.AddComponent<SpriteRenderer>();
            renderer.sprite = _frames[0];

            CopySorting(mesh, renderer);

            CribVisualContext context = new CribVisualContext
            {
                Member = member,
                Overlay = overlay,
                Renderer = renderer,
                MeshRoot = mesh != null ? mesh.gameObject : null,
                FrameIndex = 0,
                NextFrameAt = Time.time + FrameDurationSeconds
            };

            SyncToCurrentMesh(context);
            return context;
        }

        private void SyncToCurrentMesh(CribVisualContext context)
        {
            if (context == null || context.Member == null || context.Overlay == null) return;

            CharacterMesh mesh = GetCharacterMesh(context.Member);
            if (mesh != null && mesh.gameObject != context.MeshRoot)
            {
                RestoreRenderers(context);
                context.MeshRoot = mesh.gameObject;
                CopySorting(mesh, context.Renderer);
            }

            if (mesh != null && mesh.transform != null)
            {
                Transform parent = mesh.transform.parent != null ? mesh.transform.parent : context.Member.transform;
                if (context.Overlay.transform.parent != parent)
                {
                    context.Overlay.transform.parent = parent;
                }

                context.Overlay.layer = mesh.gameObject.layer;
                context.Overlay.transform.localPosition = mesh.transform.localPosition;
                context.Overlay.transform.localRotation = mesh.transform.localRotation;
                context.Overlay.transform.localScale = Vector3.one;
            }
            else
            {
                context.Overlay.transform.parent = context.Member.transform;
                context.Overlay.transform.localPosition = Vector3.zero;
                context.Overlay.transform.localRotation = Quaternion.identity;
                context.Overlay.transform.localScale = Vector3.one;
            }
        }

        private void HideCurrentMesh(CribVisualContext context)
        {
            if (context == null || context.Member == null) return;

            Renderer overlayRenderer = context.Renderer;
            Renderer[] renderers = null;

            if (context.MeshRoot != null)
            {
                renderers = context.MeshRoot.GetComponentsInChildren<Renderer>(true);
            }
            else
            {
                renderers = context.Member.GetComponentsInChildren<Renderer>(true);
            }

            if (renderers == null) return;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer == overlayRenderer) continue;
                if (context.Overlay != null && renderer.transform.IsChildOf(context.Overlay.transform)) continue;

                if (renderer.enabled && !context.DisabledRenderers.Contains(renderer))
                {
                    context.DisabledRenderers.Add(renderer);
                }

                renderer.enabled = false;
            }
        }

        private void AdvanceAnimation(CribVisualContext context)
        {
            if (context == null || context.Renderer == null || _frames == null || _frames.Length == 0) return;
            if (Time.time < context.NextFrameAt) return;

            context.FrameIndex = (context.FrameIndex + 1) % _frames.Length;
            context.Renderer.sprite = _frames[context.FrameIndex];
            context.NextFrameAt = Time.time + FrameDurationSeconds;
        }

        private void RemoveVisual(int id)
        {
            CribVisualContext context;
            if (!_activeVisuals.TryGetValue(id, out context)) return;

            RestoreRenderers(context);
            if (context.Overlay != null)
            {
                GameObject.Destroy(context.Overlay);
            }

            _activeVisuals.Remove(id);
        }

        private void RestoreRenderers(CribVisualContext context)
        {
            if (context == null) return;

            foreach (Renderer renderer in context.DisabledRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            context.DisabledRenderers.Clear();
        }

        private CharacterMesh GetCharacterMesh(FamilyMember member)
        {
            if (member == null) return null;

            try
            {
                return Traverse.Create(member).Field("m_mesh").GetValue<CharacterMesh>();
            }
            catch (Exception ex)
            {
                if (_log.IsDebugEnabled) _log.Debug($"Could not read child mesh for crib visual: {ex.Message}");
                return null;
            }
        }

        private void CopySorting(CharacterMesh mesh, SpriteRenderer target)
        {
            if (mesh == null || target == null) return;

            try
            {
                SpriteRenderer source = mesh.GetComponentInChildren<SpriteRenderer>();
                if (source == null) return;

                target.sortingLayerID = source.sortingLayerID;
                target.sortingOrder = source.sortingOrder + 1;
            }
            catch (Exception ex)
            {
                if (_log.IsDebugEnabled) _log.Debug($"Could not copy crib sprite sorting: {ex.Message}");
            }
        }

        private bool TryLoadFrames()
        {
            if (_frames != null && _frames.Length == FrameCount) return true;
            if (_loadAttempted) return false;

            _loadAttempted = true;

            try
            {
                Assembly assembly = typeof(BabyCribVisualManager).Assembly;
                using (Stream stream = assembly.GetManifestResourceStream(ResourceName))
                {
                    if (stream == null)
                    {
                        _log.Error($"Missing embedded crib sprite sheet resource '{ResourceName}'.");
                        return false;
                    }

                    byte[] bytes = ReadAllBytes(stream);
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    if (!texture.LoadImage(bytes))
                    {
                        _log.Error("Failed to decode embedded crib sprite sheet.");
                        return false;
                    }

                    texture.filterMode = FilterMode.Point;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    _sheetTexture = texture;

                    Sprite[] frames = new Sprite[FrameCount];
                    for (int i = 0; i < FrameCount; i++)
                    {
                        int x = SheetMargin + i * (FrameWidth + SheetSpacing);
                        Rect rect = new Rect(x, SheetMargin, FrameWidth, FrameHeight);
                        frames[i] = Sprite.Create(texture, rect, new Vector2(0.5f, 0f), PixelsPerUnit);
                    }

                    _frames = frames;
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to load crib visual frames: {ex.Message}");
                return false;
            }
        }

        private static byte[] ReadAllBytes(Stream stream)
        {
            byte[] buffer = new byte[stream.Length];
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read <= 0) break;
                offset += read;
            }

            return buffer;
        }
    }
}
