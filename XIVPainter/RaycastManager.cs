﻿using Dalamud.Game;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;

namespace XIVPainter;

internal static class RaycastManager
{
    const int MaxDistance = 80;
    const int compacity = MaxDistance * MaxDistance * 400;
    class Vector2Comparer : IComparer<Vector2>
    {
        public int Compare(Vector2 x, Vector2 y)
        {
            var xX = (int)x.X;
            var xY = (int)x.Y;

            var yX = (int)y.X;
            var yY = (int)y.Y;

            int com;

            //com = (xX / 10).CompareTo(yX / 10);
            //if (com != 0) return com;
            //com = (xY / 10).CompareTo(yY / 10);
            //if (com != 0) return com;

            com = xX.CompareTo(yX);
            if (com != 0) return com;
            com = xY.CompareTo(yY);
            if (com != 0) return com;

            com = x.X.CompareTo(y.X);
            if(com != 0) return com;
            return x.Y.CompareTo(y.Y);
        }
    }

    static readonly Vector2Comparer _comparer = new Vector2Comparer();

    static SortedList<Vector2, float> _rayRelay = new (compacity + 2000, _comparer);

    static readonly object _calculatingPtsLock = new object();
    static readonly Queue<Vector3> _calculatingPts = new ();
    static bool _canAdd = false;

    public static void Enable()
    {
        if(XIVPainter._framework != null)
        {
            XIVPainter._framework.Update += Update;
        }
    }

    public static void Dispose()
    {

        if (XIVPainter._framework != null)
            XIVPainter._framework.Update -= Update;
    }

    static bool _isUpdateRun = false;
    static bool _lastCanAdd = false;
    private static void Update(Framework framework)
    {
        if (_isUpdateRun) return;
        _isUpdateRun = true;
        Task.Run(() =>
        {
            if(XIVPainter._clientState != null && XIVPainter._clientState.LocalPlayer != null)
            {
                var loc = XIVPainter._clientState.LocalPlayer.Position;
                var pos = GetKey(loc);
                while (_rayRelay.Count > compacity)
                {
                    var removed = _rayRelay.MaxBy(p => Vector2.Distance(p.Key, pos));
                    _rayRelay.Remove(removed.Key);
                }
            }

            _canAdd = !_calculatingPts.Any();

            var addPts = _canAdd && _lastCanAdd;
            _lastCanAdd = _canAdd;

            if (addPts)
            {
                //Add some points!
            }

            _isUpdateRun = false;
        });
    }

    public static bool Raycast(Vector3 point, float height, out Vector3 territoryPt)
    {
        var xy = GetKey(point);
        territoryPt = point;

        //Start RayCasting!
        if (_canAdd)
        {
            lock (_calculatingPtsLock)
            {
                _calculatingPts.Enqueue(point);
            }
            RunRaycast();
        }

        if (!GetHeight(xy, out var vector)) vector = territoryPt.Y;
        if (float.IsNaN(vector))
        {
            return false;
        }
        else
        {
            territoryPt.Y = vector;
            territoryPt.Y = Math.Max(territoryPt.Y, point.Y - height);
            territoryPt.Y = Math.Min(territoryPt.Y, point.Y + height);
            return true;
        }
    }

    static FieldInfo _keyInfo;
    private static bool GetHeight(Vector2 xy, out float height)
    {
        height = 0;

        if (_rayRelay.Count > 0)
        {
            _keyInfo ??= _rayRelay.GetType().GetRuntimeFields().First(f => f.Name == "keys");
            var keys = (Vector2[])_keyInfo.GetValue(_rayRelay);
            var index = Array.BinarySearch(keys, 0, _rayRelay.Count, xy, _comparer);
            if (index < 0) index = -1 - index;
            index %= _rayRelay.Count;

            if (Vector2.Distance(keys[index], xy) > 3) return false;
            height = _rayRelay.Values[index];
            return true;
        }
        return false;
    }

    private static Vector2 GetKey(Vector3 point) 
        => new Vector2(float.Round(point.X, 1), float.Round(point.Z, 1));

    static bool _isRaycastRun = false;
    static void RunRaycast()
    {
        if(_isRaycastRun) return;
        _isRaycastRun = true;

        Task.Run(() =>
        {
            while(Dequeue(out var vector))
            {
                var key = GetKey(vector);
                var value = Raycast(vector);

                _rayRelay[key] = value;
            }
            _isRaycastRun = false;
        });
    }

    static bool Dequeue(out Vector3 vector)
    {
        lock (_calculatingPtsLock)
        {
            return _calculatingPts.TryDequeue(out vector);
        }
    }

    static unsafe float Raycast(Vector3 point)
    {
        int* unknown = stackalloc int[] { 0x4000, 0, 0x4000, 0 };

        RaycastHit hit = default;

        return FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->BGCollisionModule
            ->RaycastEx(&hit, point + Vector3.UnitY * 8, -Vector3.UnitY, 100, 1, unknown) ? hit.Point.Y : float.NaN;
    }
}