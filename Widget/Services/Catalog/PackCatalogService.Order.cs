using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KillConfirmGameBar.Services
{
    public static partial class PackCatalogService
    {
        private static List<T> OrderPacks<T>(IEnumerable<T> items, Func<T, string> key, List<string> order)
        {
            var ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (order != null)
                foreach (string id in order)
                    if (!string.IsNullOrEmpty(id) && !ranks.ContainsKey(id)) ranks[id] = ranks.Count;
            return items.OrderBy(item => ranks.TryGetValue(key(item), out int rank) ? rank : int.MaxValue)
                .ThenBy(item => GameStyleService.GetStyleForPackKey(key(item)) == GameStyleMode.Valorant
                    ? ValorantPackService.GetDisplayOrder(key(item)) : 0).ToList();
        }

        public static async Task<int> ReorderPackAsync(string key, bool voice, string targetKey, bool after)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(targetKey)) return -1;
            PackCatalog catalog = await LoadAsync();
            await CatalogIoLock.WaitAsync();
            try
            {
                GameStyleMode style = GameStyleService.GetStyleForPackKey(key);
                var keys = voice
                    ? OrderPacks(catalog.VoicePacks, p => p.Key, catalog.VoicePackOrder).Select(p => p.Key)
                    : OrderPacks(catalog.IconPacks, p => p.Key, catalog.IconPackOrder).Select(p => p.Key);
                var group = keys.Where(id => GameStyleService.GetStyleForPackKey(id) == style).ToList();
                int index = group.FindIndex(id => string.Equals(id, key, StringComparison.OrdinalIgnoreCase));
                int target = group.FindIndex(id => string.Equals(id, targetKey, StringComparison.OrdinalIgnoreCase));
                if (index < 0 || target < 0) return -1;
                if (index == target) return index;
                target += after ? 1 : 0;
                if (index < target) target--;
                if (index == target) return index;
                string sourceKey = group[index];
                group.RemoveAt(index);
                group.Insert(target, sourceKey);
                List<string> previous = voice ? catalog.VoicePackOrder : catalog.IconPackOrder;
                var next = (previous ?? new List<string>())
                    .Where(id => !string.IsNullOrEmpty(id) && GameStyleService.GetStyleForPackKey(id) != style).ToList();
                next.AddRange(group);
                if (voice) catalog.VoicePackOrder = next;
                else catalog.IconPackOrder = next;
                await SaveCoreAsync(catalog, notify: true);
                return target;
            }
            finally { CatalogIoLock.Release(); }
        }
    }
}
