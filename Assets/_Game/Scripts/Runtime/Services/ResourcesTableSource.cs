using System.IO;
using Game.Core.Data;
using UnityEngine;

namespace Game.Runtime
{
    /// The tables ship as TextAssets under Resources/Data (ARCHITECTURE §14.2); Core reads them through this.
    public sealed class ResourcesTableSource : ITableSource
    {
        public string ReadText(string tableName)
        {
            var asset = Resources.Load<TextAsset>("Data/" + tableName);
            if (asset == null)
                throw new FileNotFoundException("Resources/Data/" + tableName + ".json is missing");
            return asset.text;
        }
    }
}
