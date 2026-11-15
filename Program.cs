using System;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using Iced.Intel;

class Program
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("=== CONG CU PHAN TICH CHUYEN SAU FILE .DLL C++ (PHIEN BAN NANG CAP) ===");
        Console.Write("Nhap duong dan file .dll: ");

        string duongDan = Console.ReadLine()?.Trim('"') ?? "";
        if (!File.Exists(duongDan))
        {
            Console.WriteLine("[LOI] Khong tim thay file!");
            return;
        }

        // Lưu file kết quả ngay tại thư mục hiện tại của dự án (không làm bẩn thư mục game)
        string tenFileGoc = Path.GetFileNameWithoutExtension(duongDan);
        string thuMucHienTai = Directory.GetCurrentDirectory();
        string fileHopNgu = Path.Combine(thuMucHienTai, $"{tenFileGoc}_HopNgu_GanNhan.txt");
        string fileChuoi = Path.Combine(thuMucHienTai, $"{tenFileGoc}_ChuoiVanBan.txt");

        using var fs = new FileStream(duongDan, FileMode.Open, FileAccess.Read);
        using var peReader = new PEReader(fs);

        var headers = peReader.PEHeaders;
        int bitness = headers.CoffHeader.Machine == Machine.Amd64 ? 64 : 32;
        ulong imageBase = headers.PEHeader?.ImageBase ?? 0x180000000;

        // 1. Dịch ngược mã máy và tự động gắn nhãn các lệnh đặc biệt
        var textSec = headers.SectionHeaders.FirstOrDefault(s => s.Name == ".text");
        byte[] bytes = peReader.GetSectionData(textSec.VirtualAddress).GetContent().ToArray();

        var codeReader = new ByteArrayCodeReader(bytes);
        var decoder = Iced.Intel.Decoder.Create(bitness, codeReader);
        decoder.IP = imageBase + (ulong)textSec.VirtualAddress;

        var formatter = new IntelFormatter();
        var output = new StringOutput();
        var sbAsm = new StringBuilder();

        sbAsm.AppendLine($"=== DICH NGUOC & GAN NHAN TU DONG ({bitness}-BIT): {Path.GetFileName(duongDan)} ===\n");

        int dem = 0;
        while (codeReader.CanReadByte && dem < 5000)
        {
            decoder.Decode(out var instr);
            formatter.Format(instr, output);
            string lenhText = output.ToStringAndReset();

            // Tự động gắn nhãn giải thích cho các lệnh phần cứng đặc biệt
            string ghiChu = "";
            if (lenhText.Contains("cpuid")) ghiChu = "  <-- [KIEM TRA CPU / MAY AO]";
            else if (lenhText.Contains("rdtsc")) ghiChu = "  <-- [DO THOI GIAN CHONG DEBUG]";
            else if (lenhText.Contains("rdrand") || lenhText.Contains("rdseed")) ghiChu = "  <-- [TAO MA NGAU NHIEN PHAN CUNG]";
            else if (lenhText.Contains("lock ")) ghiChu = "  <-- [KHOA BO NHO DA LUONG]";

            sbAsm.AppendLine($"0x{instr.IP:X16}: {lenhText,-35}{ghiChu}");
            dem++;
        }
        File.WriteAllText(fileHopNgu, sbAsm.ToString());

        // 2. Quét toàn bộ file để trích xuất các chuỗi văn bản ASCII ẩn (dài từ 6 ký tự trở lên)
        byte[] toanBoFile = File.ReadAllBytes(duongDan);
        var sbStr = new StringBuilder();
        var chuoiTam = new StringBuilder();
        int tongChuoi = 0;

        sbStr.AppendLine($"=== DANH SACH CAC CHUOI VAN BAN AN TRONG FILE: {Path.GetFileName(duongDan)} ===\n");
        foreach (byte b in toanBoFile)
        {
            if (b >= 32 && b <= 126) // Ký tự chữ/số đọc được
            {
                chuoiTam.Append((char)b);
            }
            else
            {
                if (chuoiTam.Length >= 6)
                {
                    sbStr.AppendLine(chuoiTam.ToString());
                    tongChuoi++;
                }
                chuoiTam.Clear();
            }
        }
        File.WriteAllText(fileChuoi, sbStr.ToString());

        Console.WriteLine($"\n[THANH CONG] Da phan tich xong toan bo file!");
        Console.WriteLine($"1. File Hop ngu co gan nhan: {fileHopNgu}");
        Console.WriteLine($"2. File Chuoi van ban an ({tongChuoi} chuoi): {fileChuoi}");
    }
}