using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Infrastructure.Utils
{
    internal class NBPResponseUtils
    {
        public static T GetResponseContent<T>(string content)
        {
            if (!String.IsNullOrEmpty(content))
            {
                var result = JsonConvert.DeserializeObject<T>(content);
                return result;
            }
            else
            {
                return default;
            }
        }
    }
}
