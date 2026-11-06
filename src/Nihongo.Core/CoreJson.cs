using System.Text.Json.Serialization;

namespace Nihongo.Core;

/// <summary>JSON sinh code sẵn để app vẫn chạy đúng khi đóng gói có cắt gọn (trim).</summary>
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<int, int>))]
internal partial class CoreJson : JsonSerializerContext;
