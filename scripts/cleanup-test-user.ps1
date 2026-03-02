param(
    [Parameter(Mandatory = $true)]
    [string]$Email,

    [string]$ConnectionString = ""
)

$ErrorActionPreference = "Stop"

function Resolve-ConnectionString {
    param([string]$ProvidedConnectionString)

    if (-not [string]::IsNullOrWhiteSpace($ProvidedConnectionString)) {
        return $ProvidedConnectionString
    }

    $envConnectionString = $env:ConnectionStrings__DefaultConnection
    if (-not [string]::IsNullOrWhiteSpace($envConnectionString)) {
        return $envConnectionString
    }

    $appSettingsPath = Join-Path $PSScriptRoot "..\EcoTrails.Api\appsettings.json"
    if (-not (Test-Path $appSettingsPath)) {
        throw "Unable to resolve connection string. File not found: $appSettingsPath"
    }

    $json = Get-Content -Path $appSettingsPath -Raw | ConvertFrom-Json
    $resolved = $json.ConnectionStrings.DefaultConnection

    if ([string]::IsNullOrWhiteSpace($resolved)) {
        throw "DefaultConnection is missing in EcoTrails.Api/appsettings.json"
    }

    return [string]$resolved
}

$resolvedConnectionString = Resolve-ConnectionString -ProvidedConnectionString $ConnectionString
$normalizedEmail = $Email.Trim().ToUpperInvariant()

$connection = New-Object System.Data.SqlClient.SqlConnection($resolvedConnectionString)
$connection.Open()
$transaction = $connection.BeginTransaction()

try {
    $getUserId = $connection.CreateCommand()
    $getUserId.Transaction = $transaction
    $getUserId.CommandText = "SELECT TOP(1) Id FROM AspNetUsers WHERE NormalizedEmail = @normalizedEmail"
    [void]$getUserId.Parameters.Add("@normalizedEmail", [System.Data.SqlDbType]::NVarChar, 256)
    $getUserId.Parameters["@normalizedEmail"].Value = $normalizedEmail

    $userId = $getUserId.ExecuteScalar()

    if ($null -eq $userId -or [string]::IsNullOrWhiteSpace([string]$userId)) {
        $transaction.Rollback()
        Write-Host "No user found for email: $Email"
        exit 0
    }

    $cleanupSql = @"
IF OBJECT_ID('AssistantChatEntries', 'U') IS NOT NULL
BEGIN
    DELETE ace
    FROM AssistantChatEntries ace
    INNER JOIN AssistantChatSessions acs ON acs.Id = ace.SessionInternalId
    WHERE acs.AppUserId = @userId;
END

IF OBJECT_ID('AssistantChatSessions', 'U') IS NOT NULL
BEGIN
    DELETE FROM AssistantChatSessions WHERE AppUserId = @userId;
END

IF OBJECT_ID('UserFavoriteTrails', 'U') IS NOT NULL
BEGIN
    DELETE FROM UserFavoriteTrails WHERE UserId = @userId;
END

IF OBJECT_ID('AspNetUserClaims', 'U') IS NOT NULL
BEGIN
    DELETE FROM AspNetUserClaims WHERE UserId = @userId;
END

IF OBJECT_ID('AspNetUserLogins', 'U') IS NOT NULL
BEGIN
    DELETE FROM AspNetUserLogins WHERE UserId = @userId;
END

IF OBJECT_ID('AspNetUserTokens', 'U') IS NOT NULL
BEGIN
    DELETE FROM AspNetUserTokens WHERE UserId = @userId;
END

IF OBJECT_ID('AspNetUserRoles', 'U') IS NOT NULL
BEGIN
    DELETE FROM AspNetUserRoles WHERE UserId = @userId;
END

DELETE FROM AspNetUsers WHERE Id = @userId;
"@

    $cleanup = $connection.CreateCommand()
    $cleanup.Transaction = $transaction
    $cleanup.CommandText = $cleanupSql
    [void]$cleanup.Parameters.Add("@userId", [System.Data.SqlDbType]::NVarChar, 450)
    $cleanup.Parameters["@userId"].Value = [string]$userId

    [void]$cleanup.ExecuteNonQuery()

    $transaction.Commit()
    Write-Host "Deleted user and related data for: $Email"
    exit 0
}
catch {
    try { $transaction.Rollback() } catch {}
    throw
}
finally {
    $connection.Dispose()
}
