namespace EcoTrails.Api.Models;

public class AdminPanelOptions
{
    /// <summary>
    /// Admin panel username. Override with environment variable AdminPanel__Username in production.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Admin panel password. Override with environment variable AdminPanel__Password in production.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
