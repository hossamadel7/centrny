using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class ItemType
{
    public int ItemTypeCode { get; set; }

    public string ItemTypeName { get; set; } = null!;

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();
}
