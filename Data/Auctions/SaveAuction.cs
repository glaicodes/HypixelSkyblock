using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace hypixel
{

    [MessagePackObject]
    public class SaveAuction
    {
        [IgnoreMember]
        [JsonIgnore]
        public int Id { get; set; }
        [Key(0)]
        //[JsonIgnore]
        [JsonProperty("uuid")]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        public string Uuid { get; set; }

        [Key(1)]
        [JsonIgnore]
        public bool Claimed { get; set; }

        [Key(2)]
        [JsonProperty("count")]
        public int Count { get; set; }

        [Key(3)]
        [JsonProperty("startingBid")]
        public long StartingBid { get; set; }

        [Key(4)]
        public string OldTier
        {
            set
            {
                if (value == null)
                    return;
                Tier = (Tier)Enum.Parse(typeof(Tier), value, true);
            }
        }


        [Key(5)]
        public string OldCategory
        {
            set
            {
                if (value == null)
                    return;
                Category = (Category)Enum.Parse(typeof(Category), value, true);
            }
        }

        private string _tag;
        [Key(6)]
        [System.ComponentModel.DataAnnotations.MaxLength(40)]
        [JsonProperty("tag")]
        public string Tag
        {
            get
            {
                return _tag;
            }
            set
            {
                _tag = value.Truncate(40);
            }
        }
        // [Key (7)]
        //public string ItemLore;

        private string _itemName;
        [Key(8)]
        [System.ComponentModel.DataAnnotations.MaxLength(45)]
        [MySql.Data.EntityFrameworkCore.DataAnnotations.MySqlCharset("utf8")]
        [JsonProperty("itemName")]
        public string ItemName { get { return _itemName; } set { _itemName = value?.Substring(0, Math.Min(value.Length, 45)); } }

        [Key(9)]
        [JsonProperty("start")]
        public DateTime Start { get; set; }

        [Key(10)]
        [JsonProperty("end")]
        public DateTime End { get; set; }

        [Key(11)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        [JsonProperty("auctioneerId")]
        public string AuctioneerId { get; set; }
        /*    [IgnoreMember]
            [System.ComponentModel.DataAnnotations.Schema.ForeignKey("AuctioneerId")]
            public Player Auctioneer {get;set;} */

        /// <summary>
        /// is <see cref="null"/> if it is the same as the <see cref="Auctioneer"/>
        /// </summary>
        [Key(12)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        [JsonProperty("profileId")]
        public string ProfileId { get; set; }
        /// <summary>
        /// All ProfileIds of the coop members without the <see cref="Auctioneer"/> because it would be redundant
        /// </summary>
        [Key(13)]
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public List<string> Coop
        {
            set
            {
                CoopMembers = value?.Select(s => new UuId(s ?? AuctioneerId)).ToList();
            }
        }
        [IgnoreMember]
        public List<UuId> CoopMembers { get; set; }


        [Key(14)]
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [JsonIgnore]
        public List<object> ClaimedBidders
        {
            set
            {
                ClaimedBids = value?.Select(s => new UuId((string)s)).ToList();
            }
        }
        [IgnoreMember]
        [JsonIgnore]
        public List<UuId> ClaimedBids { get; set; }


        [Key(15)]
        [JsonProperty("highestBidAmount")]
        public long HighestBidAmount { get; set; }

        [Key(16)]
        [JsonProperty("bids")]
        public List<SaveBids> Bids { get; set; }
        [Key(17)]
        [JsonProperty("anvilUses")]
        public short AnvilUses { get; set; }
        [Key(18)]
        [JsonProperty("enchantments")]
        public List<Enchantment> Enchantments { get; set; }

        [Key(19)]
        [JsonProperty("nbtData")]
        public NbtData NbtData { get; set; }
        [Key(20)]
        [JsonProperty("itemCreatedAt")]
        public DateTime ItemCreatedAt { get; set; }
        [Key(21)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "TINYINT(2)")]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("reforge")]
        public ItemReferences.Reforge Reforge { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("category")]
        [Key(22)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "TINYINT(2)")]
        public Category Category { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        [Key(23)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "TINYINT(2)")]
        [JsonProperty("tier")]
        public Tier Tier { get; set; }

        [Key(24)]
        [JsonProperty("bin")]
        public bool Bin { get; set; }
        [Key(25)]
        [JsonIgnore]
        public int SellerId { get; set; }
        [IgnoreMember]
        [JsonIgnore]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "MEDIUMINT(9)")]
        public int ItemId { get; set; }
        [IgnoreMember]
        [JsonIgnore]
        public List<NBTLookup> NBTLookup { get; set; }
        private Dictionary<string,string> _flatenedNBT;
        [IgnoreMember]
        [JsonProperty("flatNbt")]
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public Dictionary<string, string> FlatenedNBT
        {
            get
            {
                if(_flatenedNBT != null)
                    return _flatenedNBT;
                try
                {
                    var data = NbtData.Data;
                    if (data == null || data.Count == 0)
                        return new Dictionary<string, string>();
                    _flatenedNBT = NBT.FlattenNbtData(data).ToDictionary(d => d.Key, d => d.Value.ToString());
                    return _flatenedNBT;
                }
                catch (Exception e)
                {
                    dev.Logger.Instance.Error(e, "getting flat NBT");
                    return null;
                }
            }
        }
        /// <summary>
        /// The first part of a uuid converted to a long for faster lookups
        /// </summary>
        [Key(26)]
        [JsonIgnore]
        public long UId { get; set; }

        public SaveAuction(string uuid)
        {
            this.Uuid = uuid;
            UId = AuctionService.Instance.GetId(uuid);
            this.End = DateTime.Now;
        }

        public SaveAuction(Hypixel.NET.SkyblockApi.Auction auction)
        {
            ClaimedBids = auction.ClaimedBidders.Select(s => new UuId((string)s)).ToList();
            Claimed = auction.Claimed;
            //ItemBytes = auction.ItemBytes;
            StartingBid = auction.StartingBid;
            if (Enum.TryParse(auction.Tier, true, out Tier tier))
                Tier = tier;
            else
                OldTier = auction.Tier;
            if (Enum.TryParse(auction.Category, true, out Category category))
                Category = category;
            else
                OldCategory = auction.Category;
            // make sure that the lenght is shorter than max
            ItemName = auction.ItemName;
            End = auction.End;
            Start = auction.Start;
            Coop = auction.Coop;

            ProfileId = auction.ProfileId == auction.Auctioneer ? null : auction.ProfileId;
            AuctioneerId = auction.Auctioneer;
            Uuid = auction.Uuid;
            HighestBidAmount = auction.HighestBidAmount;
            Bids = new List<SaveBids>();
            foreach (var item in auction.Bids)
            {
                Bids.Add(new SaveBids(item));
            }
            NBT.FillDetails(this, auction.ItemBytes);
            Bin = auction.BuyItNow; // missing from nuget package
            UId = AuctionService.Instance.GetId(auction.Uuid);
        }

        public SaveAuction(Hypixel.NET.SkyblockApi.Auctions.Auction auction)
        {
            ClaimedBids = auction.ClaimedBidders.Select(s => new UuId((string)s)).ToList();
            Claimed = auction.Claimed;
            //ItemBytes = auction.ItemBytes;
            StartingBid = auction.StartingBid;
            if (Enum.TryParse(auction.Tier, true, out Tier tier))
                Tier = tier;
            else
                OldTier = auction.Tier;
            if (Enum.TryParse(auction.Category, true, out Category category))
                Category = category;
            else
                OldCategory = auction.Category;
            // make sure that the lenght is shorter than max
            ItemName = auction.ItemName;
            End = auction.End;
            Start = auction.Start;
            Coop = auction.Coop;

            ProfileId = auction.ProfileId == auction.Auctioneer ? null : auction.ProfileId;
            AuctioneerId = auction.Auctioneer;
            Uuid = auction.Uuid;
            HighestBidAmount = auction.HighestBidAmount;
            Bids = new List<SaveBids>();
            foreach (var item in auction.Bids)
            {
                Bids.Add(new SaveBids(item));
            }
            NBT.FillDetails(this, auction.ItemBytes.Data);
            Bin = auction.BuyItNow; // missing from nuget package
        }

        public SaveAuction() { }

        public override bool Equals(object obj)
        {
            return obj is SaveAuction auction &&
                Uuid == auction.Uuid;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Uuid);

            return hash.ToHashCode();
        }
    }
}