using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Stunlock.Core;

namespace ScarletMarket.Services;

internal static class ItemSearchService {
  public static List<ItemSearchResult> SearchAllByName(string searchTerm, int maxResults = 20) {
    return SearchByNameInternal(PrefabService.AllItemPrefabNames, searchTerm, maxResults);
  }

  public static List<ItemSearchResult> SearchByName(string searchTerm, int maxResults = 20) {
    return SearchByNameInternal(PrefabService.ItemPrefabNames, searchTerm, maxResults);
  }

  private static List<ItemSearchResult> SearchByNameInternal(Dictionary<int, string> itemDictionary, string searchTerm, int maxResults = 20) {
    if (string.IsNullOrWhiteSpace(searchTerm)) {
      return [];
    }

    var results = new List<ItemSearchResult>();
    var alreadyAdded = new HashSet<string>();
    var searchTermLower = searchTerm.ToLowerInvariant().Replace("'", "");
    var searchTermNoSpaces = searchTermLower.Replace(" ", "");

    foreach (var kvp in itemDictionary) {
      var itemName = kvp.Value;

      if (ItemMatches(itemName, searchTermLower, searchTermNoSpaces) && !alreadyAdded.Contains(kvp.Value)) {
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

  private static bool ItemMatches(string itemName, string searchTermLower, string searchTermNoSpaces) {
    var (Name, Category, Tier) = ParseItemName(itemName);

    var itemNameNoCategory = Regex.Replace(itemName, "\\s*\\([^)]*\\)", "");
    var itemNameLower = itemNameNoCategory.ToLowerInvariant().Replace(" ", "").Replace("'", "");

    if (itemNameLower.Contains(searchTermLower)) {
      return true;
    }

    var searchVariations = GenerateSearchVariations(Name, Category, Tier);

    foreach (var variation in searchVariations) {
      if (DoesVariationMatch(variation, searchTermNoSpaces)) {
        return true;
      }
    }

    return false;
  }

  private static bool DoesVariationMatch(string variation, string searchTerm) {
    if (variation.Equals(searchTerm, StringComparison.OrdinalIgnoreCase)) {
      return true;
    }

    if (searchTerm.StartsWith("t") && searchTerm.Length <= 3) {
      return false;
    }

    return variation.Contains(searchTerm);
  }

  private static (string Name, string Category, string Tier) ParseItemName(string itemName) {
    var name = itemName;
    var category = "";
    var tier = "";
    var match = Regex.Match(itemName, @"^(.+?)\s*\(([^)]+)\)$");

    if (match.Success) {
      name = match.Groups[1].Value.Trim();
      var parenthesesContent = match.Groups[2].Value.Trim();
      var tierMatch = Regex.Match(parenthesesContent, @"T(\d{1,2})");
      if (tierMatch.Success) {
        tier = tierMatch.Groups[1].Value;
        category = Regex.Replace(parenthesesContent, @"\s*T\d{1,2}\s*", "").Trim();
      } else {
        category = parenthesesContent;
      }
    }

    return (name, category, tier);
  }

  private static List<string> GenerateSearchVariations(string name, string category, string tier) {
    var variations = new List<string>();
    var nameNormalized = name.ToLowerInvariant().Replace(" ", "").Replace("'", "");
    var categoryNormalized = category.ToLowerInvariant().Replace(" ", "").Replace("'", "");

    if (!string.IsNullOrEmpty(category)) {
      variations.Add(nameNormalized + categoryNormalized);
    }

    if (!string.IsNullOrEmpty(tier)) {
      var tierInt = int.Parse(tier);
      variations.Add(nameNormalized + "t" + tierInt.ToString());
      variations.Add(nameNormalized + "t" + tierInt.ToString("D2"));
      variations.Add("t" + tierInt.ToString());
      variations.Add("t" + tierInt.ToString("D2"));
    }

    if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(tier)) {
      var tierInt = int.Parse(tier);
      variations.Add(nameNormalized + categoryNormalized + "t" + tierInt.ToString());
      variations.Add(nameNormalized + categoryNormalized + "t" + tierInt.ToString("D2"));
    }

    return variations;
  }

  public static ItemSearchResult FindAllByExactName(string exactName) {
    return FindByExactNameInternal(PrefabService.AllItemPrefabNames, exactName);
  }

  public static ItemSearchResult FindByExactName(string exactName) {
    return FindByExactNameInternal(PrefabService.ItemPrefabNames, exactName);
  }

  private static ItemSearchResult FindByExactNameInternal(Dictionary<int, string> itemDictionary, string exactName) {
    if (string.IsNullOrWhiteSpace(exactName)) {
      return default;
    }

    var exactNameLower = exactName.ToLowerInvariant().Replace("'", "");
    var exactNameNoSpaces = exactNameLower.Replace(" ", "");

    foreach (var kvp in itemDictionary) {
      var itemName = kvp.Value;

      var itemNameNoParentheses = itemName.ToLowerInvariant().Replace("(", "").Replace(")", "").Replace(" ", "").Replace("'", "");
      if (itemNameNoParentheses.Equals(exactNameNoSpaces, StringComparison.OrdinalIgnoreCase)) {
        return new ItemSearchResult {
          PrefabGUID = new PrefabGUID(kvp.Key),
          Name = itemName,
          PrefabId = kvp.Key
        };
      }

      if (ItemMatchesExact(itemName, exactNameNoSpaces)) {
        return new ItemSearchResult {
          PrefabGUID = new PrefabGUID(kvp.Key),
          Name = itemName,
          PrefabId = kvp.Key
        };
      }
    }

    return default;
  }

  private static bool ItemMatchesExact(string itemName, string exactNameNoSpaces) {
    var (Name, Category, Tier) = ParseItemName(itemName);

    var searchVariations = GenerateSearchVariations(Name, Category, Tier);

    foreach (var variation in searchVariations) {
      if (variation.Equals(exactNameNoSpaces, StringComparison.OrdinalIgnoreCase)) {
        return true;
      }
    }

    return false;
  }

  public static ItemSearchResult? FindAllByPrefabGUID(PrefabGUID prefabGUID) {
    return FindAllByPrefabId(prefabGUID.GuidHash);
  }

  public static ItemSearchResult? FindAllByPrefabId(int prefabId) {
    return FindByPrefabIdInternal(PrefabService.AllItemPrefabNames, prefabId);
  }

  public static ItemSearchResult? FindByPrefabGUID(PrefabGUID prefabGUID) {
    return FindByPrefabId(prefabGUID.GuidHash);
  }

  public static ItemSearchResult? FindByPrefabId(int prefabId) {
    return FindByPrefabIdInternal(PrefabService.ItemPrefabNames, prefabId);
  }

  private static ItemSearchResult? FindByPrefabIdInternal(Dictionary<int, string> itemDictionary, int prefabId) {
    if (itemDictionary.TryGetValue(prefabId, out var name)) {
      return new ItemSearchResult {
        PrefabGUID = new PrefabGUID(prefabId),
        Name = name,
        PrefabId = prefabId
      };
    }
    return null;
  }

  public static List<ItemSearchResult> GetAllItems() {
    return GetItemsInternal(PrefabService.AllItemPrefabNames);
  }

  public static List<ItemSearchResult> GetCurrencyItems() {
    return GetItemsInternal(PrefabService.ItemPrefabNames);
  }

  private static List<ItemSearchResult> GetItemsInternal(Dictionary<int, string> itemDictionary) {
    var results = new List<ItemSearchResult>();

    foreach (var kvp in itemDictionary) {
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