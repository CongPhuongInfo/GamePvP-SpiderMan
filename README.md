# Người Giăng Tơ - Web-Slinger Co-op Online

Game bắn súng + đu dây tơ cuộn ngang, chuyển thể từ bộ khung `GamePvP-Contra`
(tái sử dụng nguyên vẹn tầng mạng P2P trong `NetworkPeer.vb`, giao thức
STATE/INPUT pipe-delimited, host authoritative). Nhân vật, quái và bối cảnh là
**thiết kế gốc**, không dùng tên/hình ảnh nhân vật thương hiệu của bên thứ ba -
chỉ lấy cảm hứng từ lối chơi "đu dây tơ giữa các tòa nhà" theo phong cách các
game siêu anh hùng nhện kinh điển.

## Điểm mới so với bản Contra: cơ chế ĐU DÂY TƠ

- Nhảy lên không trung (rơi từ mái nhà, hoặc sau khi nhảy thường) rồi **giữ phím
  đu dây tơ** (Shift/Q/. tùy chế độ) sẽ tự động bám vào **điểm neo** gần nhất
  phía trên (chấm trắng nhỏ trên nóc nhà).
- Trong lúc đu, nhấn Trái/Phải để "nhồi đà" (pump) tăng biên độ lắc, giống động
  tác lấy đà trong các game đu dây kinh điển.
- Nhả phím đu dây để thả ra bay theo quán tính; nhấn thêm phím Nhảy đúng lúc
  nhả dây sẽ được "bật" lên cao hơn một chút (giống cú nhảy-bật quen thuộc).
- Hạ cánh xuống nóc nhà/vỉa hè sẽ tự động ngắt dây.

## Chọn skin nhân vật (mới)

Trước khi vào trận, mỗi người chơi được chọn 1 trong 4 skin:

| Skin | Mô tả | File prefix |
|---|---|---|
| 0 | Đỏ - Đen (giáp công nghệ) | `player0_*.png` |
| 1 | Skin gốc thứ hai của game | `player1_*.png` |
| 2 | Đỏ - Xanh dương (kiểu người nhện cổ điển) | `player2_*.png` |
| 3 | Xanh dương - Đen | `player3_*.png` |

- **Solo**: chọn 1 lần cho nhân vật của bạn
- **2 người cùng máy**: chọn lần lượt cho Người chơi 1 rồi Người chơi 2 (có thể trùng skin nhau)
- **Host/Join qua mạng**: mỗi bên tự chọn skin của mình, được đồng bộ qua đối phương bằng message mạng `SKIN|playerIndex|skinIndex` ngay khi kết nối thành công
- Skin được lưu trong `PlayerState.SkinIndex`, đồng bộ qua giao thức STATE (host gửi) nên luôn khớp giữa 2 máy

Mỗi skin cần đủ 8 file sprite (`_walk2`, `_jump`, `_swing`, `_flip`, `_land`, `_wallcrouch`, `_shootair` + file idle gốc), thiếu file nào thì tự động dùng màu khối fallback riêng cho skin đó (đỏ, xanh dương, lục, cam).

> **Lưu ý kỹ thuật khi ghép skin mới**: engine luôn vẽ sprite bằng cách kéo giãn (stretch) vào khung cố định `PLAYER_W x PLAYER_H` (34x46, tỉ lệ dọc ~0.70-0.74), bất kể kích thước ảnh gốc. Nếu ảnh nguồn có tỉ lệ khác biệt lớn (đặc biệt ảnh nằm ngang) so với các pose còn lại của cùng skin, nhân vật sẽ bị méo/dẹp rõ rệt khi hiển thị. File `player2_shootair.png` (tư thế bắn tơ trên không của skin 2) ban đầu là ảnh nằm ngang (677x369, tỉ lệ 1.83) trong khi các pose khác của skin này là ảnh dọc (tỉ lệ ~0.70) - đã được đệm (pad) thêm nền trong suốt trên/dưới để đưa về đúng tỉ lệ dọc 0.70, giữ nguyên hình nhân vật không bị bóp méo.

## Cách chơi

- Độ phân giải màn hình: **960x540** (chuẩn 16:9)
- Màn hình mở đầu: **Chơi 1 người (Solo)**, **Host**, **Join** - giống hệt quy
  ước các bản trước.
- **Di chuyển**: mũi tên Trái/Phải hoặc A/D
- **Ngắm 8 hướng**: giữ Lên/Xuống kết hợp Trái/Phải (độc lập với hướng di chuyển)
- **Nhảy**: Space hoặc Z
- **Bắn tơ**: Ctrl hoặc X
- **Đu dây tơ** (chỉ khi đang ở trên không): giữ Shift hoặc C

## Hệ thống súng tơ (WebLevel)

- **Lv0**: bắn thường, tốc độ vừa
- **Lv1** (nhặt bình dịch tơ): bắn nhanh hơn
- **Lv2** (nhặt thêm 1 bình): bắn tỏe 3 tia cùng lúc

Nhặt bình dịch tơ hoặc +1 mạng rơi rải rác trên đường, tương tự hệ thống
powerup của bản Contra.

## Quái vật

- **Côn đồ đường phố (Thug)**: đi tuần trên vỉa hè, bắn ngang về phía người chơi
- **Xạ thủ nóc nhà (Sniper)**: đứng yên trên mái nhà, xoay ngắm theo hướng
  người chơi gần nhất (dùng nguyên kiến trúc turret của bản Contra)
- **Trùm băng đảng cuối màn (Boss)**: nhiều máu, hạ gục là thắng màn

## Kiến trúc kỹ thuật

- **WebSlingerGame.vb**: toàn bộ logic gameplay - vật lý nhảy/rơi thông thường,
  vật lý đu dây tơ (con lắc đơn giản: gia tốc góc = -(g/L)*sin(góc), có nhồi đà
  và giảm chấn), va chạm platform 1 chiều (nóc nhà), AI quái, bắn tơ, powerup,
  serialize/deserialize giao thức mạng. Host chạy `Tick()` mỗi 33ms.
- **Form1.vb**: UI Host/Join, đọc phím, vẽ GDI+ (ưu tiên sprite trong `Assets/`
  nếu có, tự động fallback hình khối màu khi thiếu file), vẽ đường dây tơ nối
  người chơi với điểm neo đang bám.
- **NetworkPeer.vb**: không đổi so với các bản trước, TCP P2P thuần túy.
- **Điểm neo (`Anchors`)**: danh sách tọa độ tĩnh, đặt cao hơn nóc nhà một chút
  dọc theo màn chơi, dùng để tìm điểm bám dây tơ gần nhất khi người chơi giữ
  phím đu dây trên không.

## Build

Chạy `build_webslinger.bat` (dùng `vbc.exe` của .NET Framework 4.x có sẵn
trong Windows, không cần Visual Studio). Sau khi build xong, đặt thư mục
`Assets/` cùng chỗ với file .exe.

## Assets

Đã có đủ 4 bộ skin nhân vật (`player0` đến `player3`), mỗi bộ 8 file sprite
theo danh sách tên file trong `LoadSpritesIfExist()` ở Form1.vb. Nếu thiếu
file, game vẫn chạy bình thường và tự động vẽ hình khối màu GDI+ thay thế.
Khi ghép thêm skin mới, nên kiểm tra kích thước/tỉ lệ ảnh khớp với các pose
còn lại của cùng skin để tránh bị kéo méo khi hiển thị (xem lưu ý ở mục
"Chọn skin nhân vật" phía trên).

## Ghi chú bản quyền

Đây là thiết kế gốc lấy cảm hứng từ cơ chế "đu dây tơ giữa nhà cao tầng" nổi
tiếng trong dòng game siêu anh hùng nhện - không sử dụng tên riêng, logo,
hoặc hình ảnh nhân vật của bất kỳ thương hiệu nào. Bạn có toàn quyền đổi tên,
màu sắc, tạo hình nhân vật theo ý thích khi làm sprite riêng.
