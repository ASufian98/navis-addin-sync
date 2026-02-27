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
        public string Error { get; set; }
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

    /// <summary>
    /// Clash detection category enum for Navisworks clash reports
    /// Values must match the API expected format exactly
    /// </summary>
    public enum ClashCategory
    {
        ArchitectureMechanical,
        ArchitectureStructure,
        ArchitectureElectrical,
        MechanicalElectrical,
        MechanicalMechanical,
        StructureElectrical,
        StructureMechanical,
        ElectricalElectrical
    }

    /// <summary>
    /// Display info for clash categories
    /// </summary>
    public class ClashCategoryInfo
    {
        public ClashCategory Category { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }

        public static List<ClashCategoryInfo> GetAllCategories()
        {
            return new List<ClashCategoryInfo>
            {
                new ClashCategoryInfo { Category = ClashCategory.ArchitectureMechanical, DisplayName = "Architecture vs Mechanical", Description = "Clashes between architectural and mechanical elements" },
                new ClashCategoryInfo { Category = ClashCategory.ArchitectureStructure, DisplayName = "Architecture vs Structure", Description = "Clashes between architectural and structural elements" },
                new ClashCategoryInfo { Category = ClashCategory.ArchitectureElectrical, DisplayName = "Architecture vs Electrical", Description = "Clashes between architectural and electrical elements" },
                new ClashCategoryInfo { Category = ClashCategory.MechanicalElectrical, DisplayName = "Mechanical vs Electrical", Description = "Clashes between mechanical and electrical elements" },
                new ClashCategoryInfo { Category = ClashCategory.MechanicalMechanical, DisplayName = "Mechanical vs Mechanical", Description = "Clashes within mechanical elements" },
                new ClashCategoryInfo { Category = ClashCategory.StructureElectrical, DisplayName = "Structure vs Electrical", Description = "Clashes between structural and electrical elements" },
                new ClashCategoryInfo { Category = ClashCategory.StructureMechanical, DisplayName = "Structure vs Mechanical", Description = "Clashes between structural and mechanical elements" },
                new ClashCategoryInfo { Category = ClashCategory.ElectricalElectrical, DisplayName = "Electrical vs Electrical", Description = "Clashes within electrical elements" }
            };
        }
    }

    /// <summary>
    /// Response from clash detection upload API
    /// </summary>
    public class ClashUploadResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ClashUploadData Data { get; set; }
    }

    public class ClashUploadData
    {
        public int ClashDetectionId { get; set; }
        public int Version { get; set; }
        public string TestName { get; set; }
        public List<string> ClashTest { get; set; }
        public int TotalClashes { get; set; }
        public int TotalComments { get; set; }
        public ClashStatusBreakdown StatusBreakdown { get; set; }
        public string FileUrl { get; set; }
        public string FileKey { get; set; }
        public double FileSize { get; set; }
        public string FileType { get; set; }
    }

    public class ClashStatusBreakdown
    {
        [JsonProperty("new")]
        public int New { get; set; }
        public int Active { get; set; }
        public int Reviewed { get; set; }
        public int Approved { get; set; }
        public int Resolved { get; set; }
    }
}
