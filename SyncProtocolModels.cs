using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NavisWebAppSync
{
    /// <summary>
    /// The three browsable areas of a project.
    /// </summary>
    public static class BimArea
    {
        public const string Wip = "wip";
        public const string Shared = "shared";
        public const string Published = "published";

        public static string Label(string area)
        {
            if (string.Equals(area, Shared, StringComparison.OrdinalIgnoreCase)) return "Shared";
            if (string.Equals(area, Published, StringComparison.OrdinalIgnoreCase)) return "Published";
            if (string.Equals(area, Wip, StringComparison.OrdinalIgnoreCase)) return "WIP";
            return area;
        }
    }

    public class BimFolder
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DisciplineType { get; set; }
        public string Area { get; set; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// One model in a folder. One row per lineage, not per version.
    /// </summary>
    public class BimDesign
    {
        public string DocGuid { get; set; }
        public int DesignId { get; set; }
        public string LineageId { get; set; }
        public string Name { get; set; }
        public int? VersionNumber { get; set; }
        public int? VersionCount { get; set; }
        public DateTime? UploadedAt { get; set; }
        public int? UploadedBy { get; set; }
        public string UploaderName { get; set; }
        public long? FileSize { get; set; }
        public string FileHash { get; set; }
        public string DisciplineType { get; set; }
        public string DesignStatus { get; set; }
        public string SyncSource { get; set; }
        public string UrnInBase64 { get; set; }
        public string XktConversionStatus { get; set; }

        /// <summary>False for a role that may browse but not download.</summary>
        public bool? CanDownload { get; set; }

        public bool IsDownloadable => !CanDownload.HasValue || CanDownload.Value;

        public string Area { get; set; }

        public int? PromotedFromDesignId { get; set; }
        public int? PromotedFromVersionNumber { get; set; }
        public string PromotedFromArea { get; set; }

        public bool HasPromotionMismatch =>
            PromotedFromVersionNumber.HasValue
            && VersionNumber.HasValue
            && PromotedFromVersionNumber.Value != VersionNumber.Value;

        public override string ToString() => Name;
    }

    public class BimDesignsResponse
    {
        public List<BimDesign> Designs { get; set; }
        public string Area { get; set; }
        public string NextCursor { get; set; }

        [JsonProperty("cursor")]
        public string Cursor
        {
            get { return NextCursor; }
            set { if (!string.IsNullOrEmpty(value)) NextCursor = value; }
        }

        public bool? HasMore { get; set; }
        public int? Limit { get; set; }

        [JsonIgnore]
        public bool IsPartial =>
            HasMore.HasValue ? HasMore.Value : !string.IsNullOrEmpty(NextCursor);
    }

    /// <summary>
    /// One version of a model.
    /// </summary>
    public class DesignVersion
    {
        public int DesignId { get; set; }
        public int? VersionNumber { get; set; }
        public string Name { get; set; }
        public DateTime? UploadedAt { get; set; }
        public int? UploadedBy { get; set; }
        public string UploaderName { get; set; }
        public long? FileSize { get; set; }
        public string SyncComment { get; set; }
        public string SyncSource { get; set; }
        public string DesignStatus { get; set; }
        public string UrnInBase64 { get; set; }
        public string XktConversionStatus { get; set; }

        public bool IsActive { get; set; }

        public int? RolledBackFromDesignId { get; set; }

        public int? PromotedFromDesignId { get; set; }
        public int? PromotedFromVersionNumber { get; set; }
        public string PromotedFromArea { get; set; }

        [JsonIgnore]
        public bool HasPromotionMismatch =>
            PromotedFromVersionNumber.HasValue
            && VersionNumber.HasValue
            && PromotedFromVersionNumber.Value != VersionNumber.Value;
    }

    public class DesignVersionsResponse
    {
        public List<DesignVersion> Versions { get; set; }
    }

    /// <summary>
    /// Raised when access is denied.
    /// </summary>
    public class BinaAccessDeniedException : Exception
    {
        public BinaAccessDeniedException(string message) : base(message) { }
    }
}
