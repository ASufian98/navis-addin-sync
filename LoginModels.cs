using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NavisWebAppSync
{
    public class LoginResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public long AccessTokenExpiry { get; set; }
        public int UserId { get; set; }
    }

    public class ProjectInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string BimRole { get; set; }
        public List<string> DisciplineTypes { get; set; }
    }

    public class UserProjectsResponse
    {
        public List<ProjectInfo> Projects { get; set; }
    }

    public class BimDisciplineFile
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FileUrl { get; set; }
        public string DisciplineType { get; set; }
        public long FileSize { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class BimLatestFile
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FileUrl { get; set; }
        public string Version { get; set; }
    }

    public class BimDisciplineFolder
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public BimLatestFile LatestFile { get; set; }
    }

    public class BimDiscipline
    {
        public List<BimDisciplineFolder> Folders { get; set; }
    }

    public class BimDisciplineResponse
    {
        public BimDiscipline Structure { get; set; }
        public BimDiscipline Architecture { get; set; }

        [JsonProperty("mechanical")]
        public BimDiscipline Mechanical { get; set; }

        public BimDiscipline Electrical { get; set; }
    }

    public class DownloadResult
    {
        public string DisciplineType { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}
