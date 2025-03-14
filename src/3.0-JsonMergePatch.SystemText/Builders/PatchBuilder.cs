﻿using Morcatko.AspNetCore.JsonMergePatch.Internal;
using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Morcatko.AspNetCore.JsonMergePatch.SystemText.Builders
{
	public static class PatchBuilder
	{
		private static object ToObject(this JsonElement jsonElement)
		{
			switch (jsonElement.ValueKind)
			{
				case JsonValueKind.Null: return null;
				case JsonValueKind.String: return jsonElement.GetString();
				case JsonValueKind.Number: return jsonElement.GetGenericNumber();
				case JsonValueKind.Array: return jsonElement.EnumerateArray().Select(j => j.ToObject()).ToArray();
				case JsonValueKind.True: return true;
				case JsonValueKind.False: return false;
				case JsonValueKind.Undefined:
				case JsonValueKind.Object:
				default:
					throw new NotSupportedException($"Unsupported ValueKind - {jsonElement.ValueKind}");
			}
		}

		private static object GetGenericNumber(this JsonElement jsonElement)
		{
			// Attempt to parse the JSON Element as an Int32 first
			if (jsonElement.TryGetInt32(out int int32)) return int32;

			// Failing that, parse it as a Decimal instead
			return jsonElement.GetDecimal();
		}

		private static bool IsValue(this JsonValueKind valueKind)
			=> (valueKind == JsonValueKind.False)
			|| (valueKind == JsonValueKind.True)
			|| (valueKind == JsonValueKind.Number)
			|| (valueKind == JsonValueKind.String);

		private static void AddOperation(IInternalJsonMergePatchDocument jsonMergePatchDocument, string pathPrefix, JsonElement jsonElement, JsonMergePatchOptions options)
		{
			var enumerator = jsonElement.EnumerateObject();
			while (enumerator.MoveNext())
			{
				var jsonProp = enumerator.Current;
				var jsonValue = jsonProp.Value;
				var path = pathPrefix + jsonProp.Name;
				if (jsonValue.ValueKind.IsValue() || (jsonValue.ValueKind == JsonValueKind.Null))
				{
					if (options.EnableDelete && (jsonValue.ValueKind == JsonValueKind.Null))
						jsonMergePatchDocument.AddOperation_Remove(path);
					else
						jsonMergePatchDocument.AddOperation_Replace(path, jsonValue.ToObject());
				}
				else if (jsonValue.ValueKind == JsonValueKind.Array)
					jsonMergePatchDocument.AddOperation_Replace(path, jsonValue.ToObject());
				else if (jsonValue.ValueKind == JsonValueKind.Object)
				{
					jsonMergePatchDocument.AddOperation_Add(path);
					AddOperation(jsonMergePatchDocument, path + "/", jsonValue, options);
				}
			}
		}

		static readonly Type internalJsonMergePatchDocumentType = typeof(InternalJsonMergePatchDocument<>);
		internal static IInternalJsonMergePatchDocument CreatePatchDocument(Type modelType, JsonElement jsonElement, JsonSerializerOptions jsonOptions, JsonMergePatchOptions mergePatchOptions)
		{
			var jsonMergePatchType = internalJsonMergePatchDocumentType.MakeGenericType(modelType);
			var json = jsonElement.GetRawText();
			var model = JsonSerializer.Deserialize(json, modelType, jsonOptions);

			var jsonMergePatchDocument = (IInternalJsonMergePatchDocument)Activator.CreateInstance(jsonMergePatchType, BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { model }, null);
			AddOperation(jsonMergePatchDocument, "/", jsonElement, mergePatchOptions);
			return jsonMergePatchDocument;
		}
	}
}
