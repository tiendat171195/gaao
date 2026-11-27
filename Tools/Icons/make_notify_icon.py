"""
Icon nhỏ cho thông báo Android (thanh trạng thái): dấu chân trắng trên nền trong suốt, 96x96.
Android chỉ dùng kênh alpha của icon nhỏ (icon màu sẽ thành ô vuông trắng), nên vẽ hình bằng alpha.
Chạy (từ gốc repo, không cần thư viện ngoài):
    python3 Tools/Icons/make_notify_icon.py Assets/PawFit/Art/Icon/PawFit_NotifySmall.png
"""
import struct
import sys
import zlib

SIZE = 96
SS = 4  # khử răng cưa: 4x4 điểm mẫu mỗi pixel

# đệm chân (elip) + 4 ngón (tròn): (tâm x, tâm y, bán kính x, bán kính y), y tính từ trên xuống
SHAPES = [
    (48.0, 62.0, 21.0, 18.0),
    (22.0, 38.0, 9.0, 11.0),
    (37.0, 23.0, 9.5, 11.5),
    (59.0, 23.0, 9.5, 11.5),
    (74.0, 38.0, 9.0, 11.0),
]


def inside(x, y):
    for cx, cy, rx, ry in SHAPES:
        if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0:
            return True
    return False


def main(path):
    rows = []
    for py in range(SIZE):
        row = bytearray([0])  # filter: none
        for px in range(SIZE):
            hit = 0
            for sy in range(SS):
                for sx in range(SS):
                    if inside(px + (sx + 0.5) / SS, py + (sy + 0.5) / SS):
                        hit += 1
            a = round(255 * hit / (SS * SS))
            row += bytes([255, 255, 255, a])
        rows.append(bytes(row))
    raw = b"".join(rows)

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0))  # 8 bit RGBA
    png += chunk(b"IDAT", zlib.compress(raw, 9))
    png += chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)
    print("Đã ghi", path, SIZE, "x", SIZE)


if __name__ == "__main__":
    main(sys.argv[1])
