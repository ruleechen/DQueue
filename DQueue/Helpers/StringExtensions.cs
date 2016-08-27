using Newtonsoft.Json;
using StackExchange.Redis;

namespace DQueue.Helpers
{
    public static class StringExtensions
    {
        public static string Serialize(this object content)
        {
            return JsonConvert.SerializeObject(content);
        }

        public static T Deserialize<T>(this string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return default(T);
            }

            return JsonConvert.DeserializeObject<T>(content);
        }

        public static T Deserialize<T>(this RedisValue content)
        {
            if (!content.HasValue || content.IsNull)
            {
                return default(T);
            }

            return content.ToString().Deserialize<T>();
        }

        public static string GetMD5(this string input)
        {
            return HashCodeGenerator.MD5(input);
        }

        public static string GetMD5(this RedisValue value)
        {
            if (!value.HasValue || value.IsNull)
            {
                return string.Empty;
            }

            return value.ToString().GetMD5();
        }
    }
}
