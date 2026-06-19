using System.ComponentModel.DataAnnotations;
using Nizam.Api.Core.Enums;

namespace Nizam.Api.DTOs;

public class TableDto
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int AreaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public TableShape Shape { get; set; }
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal Rotation { get; set; }
    public TableStatus Status { get; set; }
    public bool IsActive { get; set; }
}

public class TableForCreateDto
{
    [Required, Range(1, int.MaxValue)]
    public int BranchId { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int AreaId { get; set; }

    [Required, StringLength(40, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 100)]
    public int Capacity { get; set; } = 2;

    public TableShape Shape { get; set; } = TableShape.Square;

    [Range(0, 100000)] public decimal X { get; set; }
    [Range(0, 100000)] public decimal Y { get; set; }
    [Range(1, 10000)] public decimal Width { get; set; } = 100m;
    [Range(1, 10000)] public decimal Height { get; set; } = 100m;
    [Range(0, 360)]   public decimal Rotation { get; set; }
}

public class TableForUpdateDto
{
    [Required, Range(1, int.MaxValue)]
    public int AreaId { get; set; }

    [Required, StringLength(40, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 100)]
    public int Capacity { get; set; } = 2;

    public TableShape Shape { get; set; } = TableShape.Square;

    [Range(0, 100000)] public decimal X { get; set; }
    [Range(0, 100000)] public decimal Y { get; set; }
    [Range(1, 10000)] public decimal Width { get; set; } = 100m;
    [Range(1, 10000)] public decimal Height { get; set; } = 100m;
    [Range(0, 360)]   public decimal Rotation { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>The floor-plan response: one branch's areas + tables in a single payload so
/// the UI can render the editor without N+1 round-trips. Tables are listed within their
/// areas, and also flat for direct rendering.</summary>
public class FloorPlanDto
{
    public int BranchId { get; set; }
    public IReadOnlyList<FloorPlanAreaDto> Areas { get; set; } = Array.Empty<FloorPlanAreaDto>();
}

public class FloorPlanAreaDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<TableDto> Tables { get; set; } = Array.Empty<TableDto>();
}
