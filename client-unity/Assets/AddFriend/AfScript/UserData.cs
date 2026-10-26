using System;
using Newtonsoft.Json;

[Serializable]
public class UserData
{
    [JsonProperty("user_id")]
    public string userId { get; set; }

    [JsonProperty("username")]
    public string username { get; set; }

    [JsonProperty("user_tag")]
    public string userTag { get; set; }

    [JsonProperty("level")]
    public int level { get; set; }

    [JsonProperty("buddy_id")]
    public string buddyId { get; set; }
}