# Nguoi Giang To - Web-Slinger Co-op Online

Game ban sung + du day to cuon ngang, chuyen the tu bo khung `GamePvP-Contra`
(tai su dung nguyen ven tang mang P2P trong `NetworkPeer.vb`, giao thuc
STATE/INPUT pipe-delimited, host authoritative). Nhan vat, quai va boi canh la
**thiet ke goc**, khong dung ten/hinh anh nhan vat thuong hieu cua ben thu ba -
chi lay cam hung tu lo choi "du day to giua cac toa nha" theo phong cach cac
game sieu anh hung nhen kinh dien.

## Diem moi so voi ban Contra: co che DU DAY TO

- Nhay len khong trung (roi tu mai nha, hoac sau khi nhay thuong) roi **giu phim
  du day to** (Shift/Q/. tuy che do) se tu dong bam vao **diem neo** gan nhat
  phia tren (cham trang nho tren noc nha).
- Trong luc du, nhan Trai/Phai de "nhoi da" (pump) tang bien do lac, giong dong
  tac lay da trong cac game du day kinh dien.
- Nha phim du day de tha ra bay theo quan tinh; nhan them phim Nhay dung luc
  nha day se duoc "bat" len cao hon mot chut (giong cu nhay-bat quen thuoc).
- Ha canh xuong noc nha/via he se tu dong ngat day.

## Cach choi

- Do phan giai man hinh: **960x540** (chuan 16:9)
- Man hinh mo dau: **Choi 1 nguoi (Solo)**, **Host**, **Join** - giong het quy
  uoc cac ban truoc.
- **Di chuyen**: mui ten Trai/Phai hoac A/D
- **Ngam 8 huong**: giu Len/Xuong ket hop Trai/Phai (doc lap voi huong di chuyen)
- **Nhay**: Space hoac Z
- **Ban to**: Ctrl hoac X
- **Du day to** (chi khi dang o tren khong): giu Shift hoac C

## He thong sung to (WebLevel)

- **Lv0**: ban thuong, toc do vua
- **Lv1** (nhat binh dich to): ban nhanh hon
- **Lv2** (nhat them 1 binh): ban toe 3 tia cung luc

Nhat binh dich to hoac +1 mang roi ran rac tren duong, tuong tu he thong
powerup cua ban Contra.

## Quai vat

- **Con do duong pho (Thug)**: di tuan tren via he, ban ngang ve phia nguoi choi
- **Xa thu noc nha (Sniper)**: dung yen tren mai nha, xoay ngam theo huong
  nguoi choi gan nhat (dung nguyen kien truc turret cua ban Contra)
- **Trum bang dang cuoi man (Boss)**: nhieu mau, ha guc la thang man

## Kien truc ky thuat

- **WebSlingerGame.vb**: toan bo logic gameplay - vat ly nhay/roi thong thuong,
  vat ly du day to (con lac don gian: gia toc goc = -(g/L)*sin(goc), co nhoi da
  va giam chan), va cham platform 1 chieu (noc nha), AI quai, ban to, powerup,
  serialize/deserialize giao thuc mang. Host chay `Tick()` moi 33ms.
- **Form1.vb**: UI Host/Join, doc phim, ve GDI+ (uu tien sprite trong `Assets/`
  neu co, tu dong fallback hinh khoi mau khi thieu file), ve duong day to noi
  nguoi choi voi diem neo dang bam.
- **NetworkPeer.vb**: khong doi so voi cac ban truoc, TCP P2P thuan tuy.
- **Diem neo (`Anchors`)**: danh sach toa do tinh, dat cao hon noc nha mot chut
  doc theo man choi, dung de tim diem bam day to gan nhat khi nguoi choi giu
  phim du day tren khong.

## Build

Chay `build_webslinger.bat` (dung `vbc.exe` cua .NET Framework 4.x co san
trong Windows, khong can Visual Studio). Sau khi build xong, dat thu muc
`Assets/` cung cho voi file .exe.

## Assets

Chua kem sprite rieng cho game nay. Ban co the tai su dung/chinh sua file PNG
tu cac ban Mario/Contra da co (doi ten thanh player0.png, player1.png,
tile_ground.png, background.png...) hoac tu ve moi theo danh sach ten file
trong `LoadSpritesIfExist()` o Form1.vb. Neu thieu file, game van chay binh
thuong va tu dong ve hinh khoi mau GDI+ thay the.

## Ghi chu ban quyen

Day la thiet ke goc lay cam hung tu co che "du day to giua nha cao tang" noi
tieng trong dong game sieu anh hung nhen - khong su dung ten rieng, logo,
hoac hinh anh nhan vat cua bat ky thuong hieu nao. Ban co toan quyen doi ten,
mau sac, tao hinh nhan vat theo y thich khi lam sprite rieng.
