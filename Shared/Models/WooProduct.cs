using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using eCommerce.Shared.Helpers;

namespace eCommerce.Shared.Models
{
    public class WooProduct
    {
        public int? id { get; set; }
        public string name { get; set; }
        public string sku { get; set; }
        public string short_description { get; set; }
        public string description { get; set; }
        public string price { get; set; }
        public string regular_price { get; set; }
        public string stock_quantity { get; set; } = "10";
        public List<Attribute> attributes { get; set; } = new List<Attribute>();
        public List<MetaData> meta_data { get; set; } = new List<MetaData>();
        public Dimensions dimensions { get; set; } = new Dimensions();

        [JsonConverter(typeof(WeightConverter))]
        public string weight { get; set; }
        
        public List<Category> categories { get; set; } = new List<Category>();
        public string shipping_class { get; set; }
        public bool manage_stock { get; set; } = true;
    }

    public class Attribute
    {
        public int id { get; set; }

        [JsonConverter(typeof(FirstOptionConverter))]
        public string options { get; set; }

        public bool visible { get; set; } = true;
    }

    public class MetaData
    {
        public string key { get; set; }
        public object value { get; set; }
    }

    public class Dimensions
    {
        public string length { get; set; }
        public string width { get; set; }
        public string height { get; set; }
    }

    public class Category
    {
        public int id { get; set; }
    }

    public class AttributeMap
    {
        public int id { get; set; }
        public string name { get; set; }
    }
}



