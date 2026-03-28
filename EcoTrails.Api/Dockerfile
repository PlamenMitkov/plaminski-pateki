FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY EcoTrails.Api.csproj ./
RUN dotnet restore "EcoTrails.Api.csproj"

COPY . ./
RUN dotnet publish "EcoTrails.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN groupadd --gid 5678 appgroup \
	&& useradd --uid 5678 --gid appgroup --no-create-home --shell /usr/sbin/nologin appuser \
	&& chown -R appuser:appgroup /app

COPY --from=build /app/publish .

USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "EcoTrails.Api.dll"]
