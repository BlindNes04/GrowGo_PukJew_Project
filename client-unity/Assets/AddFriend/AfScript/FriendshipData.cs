using System;
using Newtonsoft.Json;

[Serializable]
public class FriendshipData
{
    [JsonProperty("friendship_id")]
    public string friendshipId { get; set; }

    [JsonProperty("requester_id")]
    public string requesterId { get; set; }

    [JsonProperty("addressee_id")]
    public string addresseeId { get; set; }

    [JsonProperty("status")]
    public string status { get; set; } // 'pending', 'accepted', 'rejected'
}