namespace eCommerce.Server.Processors.Lenovo;

public static class DataUtilsLenovo
{
    public static string ProcessWeight(string weight, Dictionary<string, string> deliveryInfo, object product_data)
    {
        if (product_data != null)
            ProcessApiWeight(product_data);
        else
        {
            try
            {
                var normalizedFamily = NormalizeUtisLenovo.NormalizeValuesList("Familia");
                
            }
        }
        //TODO: Implementar ProcessWeight
        return "";
    }
}