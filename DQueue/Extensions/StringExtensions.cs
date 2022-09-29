﻿using DQueue.Helpers;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Configuration;
using System.Text.RegularExpressions;

namespace DQueue
{
    public static class StringExtensions
    {
        public static string Serialize(this object content)
        {
            if (content == null) { return string.Empty; }
            var json = JsonConvert.SerializeObject(content);
            return json;
        }

        public static string SerializePretty(this object content)
        {
            if (content == null) { return string.Empty; }
            var json = JsonConvert.SerializeObject(content, Formatting.Indented);
            return json;
        }

        public static T Deserialize<T>(this string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return default(T);
            }

            return JsonConvert.DeserializeObject<T>(content);
        }

        public static string GetMD5(this string input)
        {
            return HashCodeGenerator.MD5(input);
        }

        public static string AddJsonField(this string json, string name, string value)
        {
            if (json.Length < 2) { return json; }
            if (json[json.Length - 1] != '}') { return json; }

            name = name.Replace("\"", "\\\"");
            value = value.Replace("\"", "\\\"");

            var comma = json.Length > 2 ? "," : string.Empty;
            var field = string.Format("{0}\"{1}\":\"{2}\"", comma, name, value);
            return json.Insert(json.Length - 1, field);
        }

        public static string RemoveJsonField(this string json, string name)
        {
            name = name.Replace("\"", "\\\"");
            name = Regex.Escape(name);

            var pattern = ",{0,1}\"" + name + "\":\"((\\\\\"|[^\"])*)\"";
            return Regex.Replace(json, pattern, string.Empty);
        }

        public static string AddEnqueueTime(this string json)
        {
            return json.AddJsonField(Constants.EnqueueTimeField, DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"));
        }

        public static string RemoveEnqueueTime(this string json)
        {
            return json.RemoveJsonField(Constants.EnqueueTimeField);
        }

        public static int? AsNullableInt(this string input)
        {
            int val;

            if (int.TryParse(input, out val))
            {
                return val;
            }

            return null;
        }

        public static TimeSpan? AsNullableTimeSpan(this string input)
        {
            var number = input.AsNullableInt();

            if (number.HasValue)
            {
                return TimeSpan.FromSeconds(number.Value);
            }

            TimeSpan result;

            // d.hh:mm:ss.fffffff
            if (TimeSpan.TryParse(input, out result))
            {
                return result;
            }

            return null;
        }

        public static QueueProvider AsQueueProvider(this string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                throw new ConfigurationErrorsException("AppSetting \"DQueue.Provider\" is required");
            }

            QueueProvider provider;

            if (Enum.TryParse(str, true, out provider))
            {
                return provider;
            }
            else
            {
                throw new ConfigurationErrorsException("AppSetting \"DQueue.Provider\" can not support provider: \"" + str + "\"");
            }
        }

        public static string GetString(this RedisValue value)
        {
            if (!value.HasValue || value.IsNull)
            {
                return string.Empty;
            }

            return value.ToString();
        }
    }
}
