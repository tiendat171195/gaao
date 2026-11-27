# Máy chủ PawFit. Dựng từ GỐC repo (cần cả code luật game trong Assets/PawFit/Scripts):
#     docker build -f Server/Dockerfile -t pawfit-server .
# Thường không gọi tay: docker-compose.yml trong thư mục này tự dựng.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Assets/PawFit/Scripts/Core ./Assets/PawFit/Scripts/Core
COPY Assets/PawFit/Scripts/Pet ./Assets/PawFit/Scripts/Pet
COPY Assets/PawFit/Scripts/Battle ./Assets/PawFit/Scripts/Battle
COPY Assets/PawFit/Scripts/Online ./Assets/PawFit/Scripts/Online
COPY Server/PawFit.Server ./Server/PawFit.Server
RUN dotnet publish Server/PawFit.Server -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# dữ liệu (SQLite) nằm trong volume /data: cập nhật / dựng lại máy chủ không mất tài khoản
ENV PawFit__DbPath=/data/pawfit.db \
    ASPNETCORE_URLS=http://0.0.0.0:5080
VOLUME /data
EXPOSE 5080
ENTRYPOINT ["dotnet", "PawFit.Server.dll"]
