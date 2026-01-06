using System;
using System.Collections.Generic;

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

    public class BimDisciplineResponse
    {
        public BimDisciplineFile Structure { get; set; }
        public BimDisciplineFile Architecture { get; set; }
        public BimDisciplineFile HVAC { get; set; }
        public BimDisciplineFile Electrical { get; set; }
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
