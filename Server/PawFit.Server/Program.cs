using Microsoft.AspNetCore.Builder;
using PawFit.Server;

// Máy chủ PawFit: cấu hình ở appsettings.json / biến môi trường (PawFit__DbPath, PawFit__BehindProxy, ...).
WebApplication app = ServerApp.Build(args);
app.Run();
