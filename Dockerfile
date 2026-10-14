# The recipe to build the API's Docker image: a sealed box with the API inside and everything
# it needs to run, so it behaves the same on any computer or server.
#
# It has TWO STAGES. The first one compiles (it needs the big .NET SDK, with the compiler and
# all its tools). The second one only RUNS the result (it needs the small ASP.NET runtime).
# Only the second one is kept: the compiler never travels to the server.

# ---------- Stage 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy ONLY the project file and download the packages. Docker keeps the result of every step
# (a "layer") and reuses it while its inputs don't change: as long as the packages in the
# .csproj stay the same, this slow step is skipped on the next builds, even if the code changed.
COPY PlaytestTracker.Api.csproj ./
RUN dotnet restore PlaytestTracker.Api.csproj

# Now the rest of the code (what .dockerignore leaves out is not copied), and publish:
# compile in Release mode and gather what the API needs to run into /app/publish
COPY . .
RUN dotnet publish PlaytestTracker.Api.csproj \
    --configuration Release --no-restore --output /app/publish

# ---------- Stage 2: the final image ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Take ONLY the published result from stage 1 (nothing else comes along)
COPY --from=build /app/publish .

# Don't run as the administrator of the box (root): if someone ever broke into the API, they
# would have the least power possible. The .NET images already include this unprivileged user.
USER $APP_UID

# The API listens on port 8080 inside the box (.NET 8 images do this by default; a non-root
# user can't use ports below 1024). EXPOSE is only a note for people: it opens nothing by itself.
EXPOSE 8080

# What runs when the box starts
ENTRYPOINT ["dotnet", "PlaytestTracker.Api.dll"]
