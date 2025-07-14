using System;
using System.Collections.Generic;
using ScarletMarket.Models;
using Unity.Mathematics;

namespace ScarletMarket.Services;

/// <summary>
/// Dynamic spatial hash table implementation for efficient collision detection and proximity queries.
/// </summary>
internal static class DynamicSpatialHash {
  private static readonly float _cellSize = 2f;
  private static readonly float _inverseCellSize = 1f / 2f;
  private static readonly Dictionary<uint, HashSet<PlotModel>> _table = [];
  private static readonly Dictionary<PlotModel, HashSet<uint>> _objectHashes = [];
  private static int _overflow = 0;

  /// <summary>
  /// Gets or sets the overflow value - adds extra cells that objects can be in.
  /// </summary>
  public static int Overflow {
    get => _overflow;
    set => _overflow = value;
  }

  /// <summary>
  /// Gets the cell size.
  /// </summary>
  public static float CellSize => _cellSize;

  /// <summary>
  /// Adds an object to the hash table.
  /// </summary>
  /// <param name="obj">The object to add</param>
  /// <param name="x">X position</param>
  /// <param name="y">Y position</param>
  /// <param name="width">Width of the object</param>
  /// <param name="height">Height of the object</param>
  public static void Add(PlotModel obj, float x, float y, float width, float height) {
    var hashes = GetHashesFromBounds(x, y, width, height);
    _objectHashes[obj] = hashes;

    foreach (var hash in hashes) {
      if (!_table.ContainsKey(hash)) {
        _table[hash] = new HashSet<PlotModel>();
      }
      _table[hash].Add(obj);
    }
  }

  /// <summary>
  /// Removes an object from the hash table.
  /// </summary>
  /// <param name="obj">The object to remove</param>
  public static void Remove(PlotModel obj) {
    if (!_objectHashes.ContainsKey(obj)) return;

    var hashes = _objectHashes[obj];
    foreach (var hash in hashes) {
      if (_table.ContainsKey(hash)) {
        _table[hash].Remove(obj);

        if (_table[hash].Count == 0) {
          _table.Remove(hash);
        }
      }
    }

    _objectHashes.Remove(obj);
  }

  /// <summary>
  /// Updates an object's position in the hash table.
  /// </summary>
  /// <param name="obj">The object to update</param>
  /// <param name="x">New X position</param>
  /// <param name="y">New Y position</param>
  /// <param name="width">Width of the object</param>
  /// <param name="height">Height of the object</param>
  public static void Update(PlotModel obj, float x, float y, float width, float height) {
    Remove(obj);
    Add(obj, x, y, width, height);
  }

  /// <summary>
  /// Queries for all objects in the same hash cells as the given bounds.
  /// </summary>
  /// <param name="x">X position</param>
  /// <param name="y">Y position</param>
  /// <param name="width">Width</param>
  /// <param name="height">Height</param>
  /// <returns>Set of objects in the same hash cells</returns>
  public static HashSet<PlotModel> Query(float x, float y, float width, float height) {
    var hashes = GetHashesFromBounds(x, y, width, height);
    var result = new HashSet<PlotModel>();

    foreach (var hash in hashes) {
      if (_table.ContainsKey(hash)) {
        foreach (var obj in _table[hash]) {
          result.Add(obj);
        }
      }
    }

    return result;
  }

  /// <summary>
  /// Queries for all objects in the same hash cells as the given object.
  /// </summary>
  /// <param name="obj">The object to query around</param>
  /// <returns>Set of objects in the same hash cells (excluding the query object)</returns>
  public static HashSet<PlotModel> Query(PlotModel obj) {
    if (!_objectHashes.ContainsKey(obj)) return new HashSet<PlotModel>();

    var hashes = _objectHashes[obj];
    var result = new HashSet<PlotModel>();

    foreach (var hash in hashes) {
      if (_table.ContainsKey(hash)) {
        foreach (var otherObj in _table[hash]) {
          if (!otherObj.Equals(obj)) {
            result.Add(otherObj);
          }
        }
      }
    }

    return result;
  }

  /// <summary>
  /// Gets all objects in a specific hash cell.
  /// </summary>
  /// <param name="hash">The hash to get objects from</param>
  /// <returns>Set of objects in the hash cell, or null if hash doesn'PlotModel exist</returns>
  public static HashSet<PlotModel> Get(uint hash) {
    return _table.ContainsKey(hash) ? _table[hash] : null;
  }

  /// <summary>
  /// Clears a specific hash cell.
  /// </summary>
  /// <param name="hash">The hash to clear</param>
  public static void Clear(uint hash) {
    _table.Remove(hash);
  }

  /// <summary>
  /// Clears all hash cells.
  /// </summary>
  public static void ClearAll() {
    _table.Clear();
    _objectHashes.Clear();
  }

  /// <summary>
  /// Gets the hash cells that the given bounds occupy.
  /// </summary>
  /// <param name="x">X position</param>
  /// <param name="y">Y position</param>
  /// <param name="width">Width</param>
  /// <param name="height">Height</param>
  /// <returns>Set of hash values</returns>
  private static HashSet<uint> GetHashesFromBounds(float x, float y, float width, float height) {
    var hashes = new HashSet<uint>();

    var minX = (int)math.floor((x - width * 0.5f) * _inverseCellSize) - _overflow;
    var minY = (int)math.floor((y - height * 0.5f) * _inverseCellSize) - _overflow;
    var maxX = (int)math.floor((x + width * 0.5f) * _inverseCellSize) + _overflow;
    var maxY = (int)math.floor((y + height * 0.5f) * _inverseCellSize) + _overflow;

    for (int cellX = minX; cellX <= maxX; cellX++) {
      for (int cellY = minY; cellY <= maxY; cellY++) {
        hashes.Add(Hash(cellX, cellY));
      }
    }

    return hashes;
  }

  /// <summary>
  /// Creates a 32-bit hash from x and y coordinates.
  /// </summary>
  /// <param name="x">The x coordinate</param>
  /// <param name="y">The y coordinate</param>
  /// <returns>A 32-bit hash value</returns>
  private static uint Hash(int x, int y) {
    return (uint)((x * 16777619) ^ (y * 16777619));
  }
}
