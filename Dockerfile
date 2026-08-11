FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia o arquivo de projeto e restaura dependências
COPY ["CTFoco.Api.csproj", "./"]
RUN dotnet restore "CTFoco.Api.csproj"

# Copia todo o código e compila
COPY . .
RUN dotnet publish "CTFoco.Api.csproj" -c Release -o /app/publish

# Imagem final de execução
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render utiliza portas dinâmicas
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CTFoco.Api.dll"]