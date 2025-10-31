using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Vosk;
using NAudio.Wave;

public class VoskMicDemo
{
    public static void Main()
    {
        // Model isimleri (senin dediğin gibi)
        const string TR_MODEL_NAME = "model-tr";
        const string EN_MODEL_NAME = "model-en";

        string choice = "";
        while (choice != "1" && choice != "2")
        {
            Console.WriteLine("Lütfen bir dil seçin:");
            Console.WriteLine("1: Türkçe");
            Console.WriteLine("2: English");
            Console.Write("Seçiminiz (1 veya 2): ");
            choice = Console.ReadLine()?.Trim() ?? "";
        }

        string wantedModelName = choice == "1" ? TR_MODEL_NAME : EN_MODEL_NAME;
        Console.WriteLine($"Aranan model: '{wantedModelName}'");

        // Çalıştırılan uygulamanın dizini
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        // Denenecek yollar listesi: çalıştırma dizini, çalışma dizini, birkaç üst dizin
        var candidatePaths = new List<string>
        {
            Path.Combine(baseDir, wantedModelName),
            Path.Combine(Directory.GetCurrentDirectory(), wantedModelName),
            Path.GetFullPath(Path.Combine(baseDir, "..", wantedModelName)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", wantedModelName)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", wantedModelName))
        };

        // Tekrarlayan eşsiz yolları koru
        var tried = new List<string>();
        string foundModelPath = null;
        foreach (var p in candidatePaths)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            string full = Path.GetFullPath(p);
            if (tried.Contains(full)) continue;
            tried.Add(full);
            if (Directory.Exists(full))
            {
                foundModelPath = full;
                break;
            }
        }

        if (foundModelPath == null)
        {
            Console.WriteLine("Model klasörü bulunamadı. Aşağıdaki yollar kontrol edildi:");
            foreach (var t in tried)
                Console.WriteLine(" - " + t);
            Console.WriteLine("\nLütfen model klasörünüzün adının ve konumunun doğru olduğundan emin olun.");
            Console.WriteLine("Örnek: projenizin kökünde model-tr veya model-en klasörü olmalı veya bin klasörüne kopyalanmış olmalı.");
            Console.WriteLine("Program sonlandırılıyor. Devam etmek için bir tuşa basın...");
            Console.ReadKey();
            return;
        }

        Console.WriteLine($"Model bulundu: {foundModelPath}");
        Console.WriteLine("Model yükleniyor...");

        var recognizedTextParts = new List<string>();

        try
        {
            Vosk.Vosk.SetLogLevel(0); // opsiyonel: log azaltma

            // Model -> Recognizer -> WaveInEvent sıralaması ve using ile dispose garantisi
            using (var model = new Model(foundModelPath))
            using (var recognizer = new VoskRecognizer(model, 16000.0f))
            using (var waveIn = new WaveInEvent { DeviceNumber = 0, WaveFormat = new WaveFormat(16000, 16, 1) })
            {
                recognizer.SetMaxAlternatives(0);
                recognizer.SetWords(true);

                waveIn.DataAvailable += (sender, args) =>
                {
                    try
                    {
                        // args.Buffer ve args.BytesRecorded uygun overload
                        if (recognizer.AcceptWaveform(args.Buffer, args.BytesRecorded))
                        {
                            string jsonResult = recognizer.Result();
                            Match match = Regex.Match(jsonResult, "\"text\"\\s*:\\s*\"(.*?)\"");
                            if (match.Success)
                            {
                                string recognizedText = match.Groups[1].Value;
                                if (!string.IsNullOrEmpty(recognizedText))
                                {
                                    recognizedTextParts.Add(recognizedText);
                                    Console.WriteLine($"'{recognizedText}' anlaşıldı ve listeye eklendi.");
                                }
                            }
                        }
                        else
                        {
                            // partial result gerekirse kullanılabilir:
                            // var partial = recognizer.PartialResult();
                        }
                    }
                    catch (Exception evex)
                    {
                        Console.WriteLine("DataAvailable içi hata: " + evex.GetType().Name + " - " + evex.Message);
                    }
                };

                waveIn.StartRecording();
                Console.WriteLine("Konuşmaya başlayabilirsiniz... Konuşmanız bittiğinde bir tuşa basın.");
                Console.ReadKey();

                waveIn.StopRecording();

                Console.WriteLine("\n--- KONUŞMANIN TAMAMI ---");
                if (recognizedTextParts.Count > 0)
                {
                    string fullTranscript = string.Join(" ", recognizedTextParts);
                    Console.WriteLine(fullTranscript);
                }
                else
                {
                    Console.WriteLine("Hiçbir şey algılanmadı.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Genel hata: " + ex.GetType().Name);
            Console.WriteLine(ex.ToString());
            Console.WriteLine("Not: Eğer AccessViolationException veya benzeri native hata alıyorsanız:");
            Console.WriteLine(" - Projenizin Platform Target'ını x64 yapın (Project Properties → Build → Platform target).");
            Console.WriteLine(" - Model klasörünün tam ve eksiksiz olduğundan emin olun.");
            Console.WriteLine(" - Vosk NuGet paketinin uyumlu bir versiyonunu kullanın.");
        }

        Console.WriteLine("\nProgram sonlandırıldı. Kapatmak için bir tuşa basın...");
        Console.ReadKey();
    }
}
