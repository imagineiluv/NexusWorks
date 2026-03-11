using System;
using Microsoft.AspNetCore.Components;

namespace SuperTutty.UI.Data;

public class LayoutChrome
{
    public RenderFragment? TitleBar { get; set; }
    public RenderFragment? Footer { get; set; }
    public string? PageTitle { get; set; }
    public bool? IsDetailPage { get; set; }
}