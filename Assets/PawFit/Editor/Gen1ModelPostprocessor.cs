using UnityEditor;

namespace PawFit.EditorTools
{
    /// <summary>
    /// Tự đặt thông số import cho model thú Gen 1 (mọi FBX trong thư mục .../Pets/Gen1/):
    /// giữ blendshape + xương, tự tính normal mượt, không tạo material (shader gán bằng code),
    /// không có animation (thú chuyển động bằng code).
    /// </summary>
    public sealed class Gen1ModelPostprocessor : AssetPostprocessor
    {
        // tăng số này khi đổi thông số bên dưới => Unity tự import lại model
        public override uint GetVersion() => 2;

        private void OnPreprocessModel()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Pets/Gen1/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importBlendShapes = true;
            mi.importVisibility = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importNormals = ModelImporterNormals.Calculate;
            mi.normalSmoothingAngle = 180f;
            mi.importBlendShapeNormals = ModelImporterNormals.Calculate;
            mi.importTangents = ModelImporterTangents.None;
            mi.weldVertices = true;
            mi.generateSecondaryUV = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.animationType = ModelImporterAnimationType.Generic;   // giữ xương thành Transform để xoay bằng code
            mi.importAnimation = false;
            mi.optimizeGameObjects = false;
        }
    }
}
