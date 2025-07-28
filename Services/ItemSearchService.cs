using System;
using System.Collections.Generic;
using System.Linq;
using Stunlock.Core;

namespace ScarletMarket.Services;

internal static class ItemSearchService {
  public static List<ItemSearchResult> SearchByName(string searchTerm, int maxResults = 20) {
    if (string.IsNullOrWhiteSpace(searchTerm)) {
      return [];
    }

    var results = new List<ItemSearchResult>();
    var alreadyAdded = new HashSet<string>();
    var searchTermLower = searchTerm.ToLowerInvariant();
    var searchTermNoSpaces = searchTermLower.Replace(" ", "");

    foreach (var kvp in PrefabService.ItemPrefabNames) {
      var itemName = kvp.Value;
      var itemNameLower = itemName.ToLowerInvariant().Replace("(", "").Replace(")", "").Replace(" ", "");

      if (itemNameLower.Contains(searchTermLower) && !alreadyAdded.Contains(kvp.Value)) {
        alreadyAdded.Add(kvp.Value);
        results.Add(new ItemSearchResult {
          PrefabGUID = new PrefabGUID(kvp.Key),
          Name = itemName,
          PrefabId = kvp.Key
        });

        if (results.Count >= maxResults) {
          break;
        }
      }
    }

    return [.. results.OrderBy(r => {
      var nameLower = r.Name.ToLowerInvariant();
      var nameNoSpaces = nameLower.Replace(" ", "");

      if (nameLower.StartsWith(searchTermLower) || nameNoSpaces.StartsWith(searchTermNoSpaces)) {
        return 0;
      }
      return 1;
    }).ThenBy(r => r.Name.Length).ThenBy(r => r.Name)];
  }


  public static ItemSearchResult? FindByExactName(string exactName) {
    if (string.IsNullOrWhiteSpace(exactName)) {
      return null;
    }

    foreach (var kvp in PrefabService.ItemPrefabNames) {
      if (kvp.Value.ToLowerInvariant().Replace("(", "").Replace(")", "").Replace(" ", "").Equals(exactName.ToLowerInvariant().Replace("(", "").Replace(")", "").Replace(" ", ""), StringComparison.OrdinalIgnoreCase)) {
        return new ItemSearchResult {
          PrefabGUID = new PrefabGUID(kvp.Key),
          Name = kvp.Value,
          PrefabId = kvp.Key
        };
      }
    }

    return null;
  }


  public static ItemSearchResult? FindByPrefabGUID(PrefabGUID prefabGUID) {
    return FindByPrefabId(prefabGUID.GuidHash);
  }


  public static ItemSearchResult? FindByPrefabId(int prefabId) {
    if (PrefabService.ItemPrefabNames.TryGetValue(prefabId, out var name)) {
      return new ItemSearchResult {
        PrefabGUID = new PrefabGUID(prefabId),
        Name = name,
        PrefabId = prefabId
      };
    }
    return null;
  }

  public static List<ItemSearchResult> GetAllItems() {
    var results = new List<ItemSearchResult>();

    foreach (var kvp in PrefabService.ItemPrefabNames) {
      results.Add(new ItemSearchResult {
        PrefabGUID = new PrefabGUID(kvp.Key),
        Name = kvp.Value,
        PrefabId = kvp.Key
      });
    }

    return results.OrderBy(r => r.Name).ToList();
  }
}

public struct ItemSearchResult {
  public PrefabGUID PrefabGUID { get; set; }
  public string Name { get; set; }
  public int PrefabId { get; set; }

  public override string ToString() {
    return $"{Name} (ID: {PrefabId})";
  }
}
