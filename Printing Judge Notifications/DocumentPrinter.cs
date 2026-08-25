using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace Printing_Judge_Notifications
{
    public class DocumentPrinter
    {
        private static (string cleaned, string secondSurname) ExtractSecondSurnameFromSegment(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return ("", null);

            int open = input.IndexOf('(');
            if (open >= 0)
            {
                int close = input.IndexOf(')', open);
                if (close > open)
                {
                    string secondSurname = input.Substring(open + 1, close - open - 1).Trim();
                    string cleaned = input.Remove(open, close - open + 1).Trim();
                    return (cleaned, secondSurname);
                }
            }
            return (input.Trim(), null);
        }

        private static (string surname, string name, string patronymicBase, string suffix) ParseNameParts(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return ("", "", "", "");

            var parts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count == 0)
                return ("", "", "", "");

            string suffix = "";
            string lastWord = parts[^1].ToLower(CultureInfo.InvariantCulture);
            if (lastWord == "оглы" || lastWord == "кызы")
            {
                suffix = parts[^1];
                parts.RemoveAt(parts.Count - 1);
            }

            if (parts.Count == 0)
                return ("", "", "", suffix);

            string surname = parts[0];
            string name = parts.Count >= 2 ? parts[1] : "";
            string patronymicBase = parts.Count > 2 ? string.Join(" ", parts.Skip(2)) : "";

            return (surname, name, patronymicBase, suffix);
        }

        // Эти методы вынесены как приватные методы класса, а не локальные функции.
        private string DeclineSafeSurname(string s) => !string.IsNullOrWhiteSpace(s) ? RussianDeclension.DeclineSurname(s) : s;
        private string DeclineSafeName(string n)   => !string.IsNullOrWhiteSpace(n) ? RussianDeclension.DeclineName(n) : n;
        private string DeclineSafePatronymic(string p) => !string.IsNullOrWhiteSpace(p) ? RussianDeclension.DeclinePatronymic(p) : p;

        public void GenerateTemplate(string templatePath, string outputPath, OwnerData data, int selectedLower, int townNumber)
        {
            // --- АДРЕС ---
            string townPrefix = "г. ";
            if (!string.IsNullOrWhiteSpace(data.Town))
            {
                string lastTwo = data.Town.Length >= 2 ? data.Town.Substring(data.Town.Length - 2) : data.Town;
                if (lastTwo == "ий") townPrefix = "пос. ";
                else if (lastTwo == "ая") townPrefix = "ст. ";
                else if (lastTwo == "ое") townPrefix = "с. ";
            }

            string addressBase = string.IsNullOrWhiteSpace(data.Street)
                ? $"{townPrefix}{data.Town}, д. {data.Building ?? ""}"
                : $"{townPrefix}{data.Town}, ул. {data.Street}, д. {data.Building ?? ""}";

            string AddPart(string current, string prefix, string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return current + prefix + value;
                return current;
            }

            string fullAddress = addressBase;
            fullAddress = AddPart(fullAddress, ", корп. ", data.Korps);
            fullAddress = AddPart(fullAddress, ", кв. ", data.Flat);
            fullAddress = AddPart(fullAddress, ", ком. ", data.Room);

            // --- ПОДГОТОВКА СПИСКА ВЛАДЕЛЬЦЕВ ---
            string fullName = data?.FullName ?? "";
            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.Show("ФИО не заполнено!");
                return;
            }

            fullName = fullName.Trim();

            List<string> segments = new List<string>();

            var partsRaw = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            int separatorIndex = partsRaw.FindIndex(p => p.Equals("за", StringComparison.OrdinalIgnoreCase));

            bool isParentChild = false; // ← НОВЫЙ ФЛАГ

            if (separatorIndex >= 0)
            {
                isParentChild = true; // ← УСТАНАВЛИВАЕМ ФЛАГ
                string parentRaw = string.Join(" ", partsRaw.Take(separatorIndex));
                string childRaw = string.Join(" ", partsRaw.Skip(separatorIndex + 1));

                if (!string.IsNullOrWhiteSpace(parentRaw)) segments.Add(parentRaw);
                if (!string.IsNullOrWhiteSpace(childRaw)) segments.Add(childRaw);
            }
            else if (fullName.Contains("/"))
            {
                var splitBySlash = fullName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var seg in splitBySlash)
                {
                    if (!string.IsNullOrWhiteSpace(seg.Trim()))
                        segments.Add(seg.Trim());
                }
            }
            else
            {
                segments.Add(fullName);
            }

            // --- ОБРАБОТКА КАЖДОГО ВЛАДЕЛЬЦА (СКЛОНЕНИЕ ПО ЧАСТЯМ) ---
            List<string> declinedNames = new List<string>();

            foreach (var segment in segments)
            {
                var (cleaned, secondSurnameRaw) = ExtractSecondSurnameFromSegment(segment);
                var parts = ParseNameParts(cleaned);

                string declSurname = DeclineSafeSurname(parts.surname);
                string declName = DeclineSafeName(parts.name);
                string declPatr = DeclineSafePatronymic(parts.patronymicBase);
                string declSecond = !string.IsNullOrEmpty(secondSurnameRaw)
                    ? DeclineSafeSurname(secondSurnameRaw)
                    : "";

                var nameParts = new List<string>();
                string finalSurname = declSurname;
                if (!string.IsNullOrEmpty(declSecond))
                    finalSurname = $"{declSurname} ({declSecond})";

                nameParts.Add(finalSurname);
                if (!string.IsNullOrEmpty(declName)) nameParts.Add(declName);
                if (!string.IsNullOrEmpty(declPatr)) nameParts.Add(declPatr);
                if (!string.IsNullOrEmpty(parts.suffix)) nameParts.Add(parts.suffix);

                declinedNames.Add(string.Join(" ", nameParts));
            }

            // --- РАЗДЕЛЕНИЕ ПРЕДСТАВИТЕЛЯ И РЕБЁНКА ---
            string genitiveCaseFullName;
            string parentFullName = "";

            if (isParentChild && declinedNames.Count >= 2)
            {
                // Представитель — первый сегмент (до "за"), ребёнок — второй
                parentFullName = declinedNames[0];
                genitiveCaseFullName = string.Join(", ", declinedNames.Skip(1));
            }
            else
            {
                genitiveCaseFullName = string.Join(", ", declinedNames);
            }

            // --- РОД (ПРОЖИВАЮЩЕГО/ПРОЖИВАЮЩЕЙ) ---
            int ownerCount = segments.Count;

            string livingAtString;
            if (isParentChild)
            {
                // Для случая «представитель за ребёнка» — берём только ребёнка (segments[1])
                string childSegment = segments.Count > 1 ? segments[1] : segments[0];
                var (cleaned, _) = ExtractSecondSurnameFromSegment(childSegment);
                var parts = ParseNameParts(cleaned);
                string checkString = (parts.patronymicBase + " " + parts.suffix).Trim();
                livingAtString = RussianDeclension.GetLivingAt(checkString, 1);
            }
            else if (ownerCount == 1)
            {
                string lastOwnerForGender = segments[0];
                var (cleaned, _) = ExtractSecondSurnameFromSegment(lastOwnerForGender);
                var parts = ParseNameParts(cleaned);
                string checkString = (parts.patronymicBase + " " + parts.suffix).Trim();
                livingAtString = RussianDeclension.GetLivingAt(checkString, 1);
            }
            else
            {
                bool? anyFemale = null;
                bool? anyMale = null;

                foreach (var segment in segments)
                {
                    var (cleaned, _) = ExtractSecondSurnameFromSegment(segment);
                    var parts = ParseNameParts(cleaned);
                    string checkString = (parts.patronymicBase + " " + parts.suffix).Trim();
                    bool? isFemale = RussianDeclension.IsFemale(checkString);

                    if (isFemale == true)
                        anyFemale = true;
                    else if (isFemale == false)
                        anyMale = true;

                    if (anyFemale == true && anyMale == true)
                    {
                        livingAtString = "проживающих по адресу: ";
                        goto EndGenderCheck;
                    }
                }

                if (anyFemale == true && anyMale == null)
                    livingAtString = "проживающих по адресу: ";
                else if (anyMale == true && anyFemale == null)
                    livingAtString = "проживающих по адресу: ";
                else
                    livingAtString = "проживающих по адресу: ";

            EndGenderCheck:;
            }

            // --- ДОКУМЕНТНЫЕ ДАННЫЕ ---
            var orderNumber = data.OrderNumber.ToString();
            string c_documentType = (orderNumber.Contains("ВС") || orderNumber.Contains("BC"))
                ? "исполнительному листу №"
                : "судебному приказу №";

            string i_documentType = (orderNumber.Contains("ВС") || orderNumber.Contains("BC"))
                ? "исполнительного листа №"
                : "судебного приказа №";

            string amountSum = data.AmountSum.ToString();

            string placement = "";
            string subjectRF = "";
            string officer = "";
            string officer_address = "";
            string officer_street = "";
            string lawyer = "";

            switch (selectedLower)
            {
                case 1: lawyer = "C. В. Ловянников"; break;
                case 2: lawyer = "О. А. Король"; break;
            }

            switch (townNumber)
            {
                case 1:
                    placement = "Курского"; subjectRF = "Ставропольскому краю";
                    officer = "А. И. Заргарову";
                    officer_address = "357850, Ставропольский край, ст. Курская";
                    officer_street = "пр. Комсомольский, 8";
                    break;
                case 2:
                    placement = "Степновского"; subjectRF = "Ставропольскому краю";
                    officer = "О. В. Балдовой";
                    officer_address = "357930, Ставропольский край, с. Степное";
                    officer_street = "пл. Ленина, 17а";
                    break;
                case 3:
                    placement = "Советского"; subjectRF = "Ставропольскому краю";
                    officer = "А. Г. Ржевскому";
                    officer_address = "357914, Ставропольский край, г. Зеленокумск";
                    officer_street = "ул. 50 лет Октября, 51";
                    break;
                case 4:
                    placement = "Георгиевского"; subjectRF = "Ставропольскому краю";
                    officer = "А. П. Капуста";
                    officer_address = "357820, Ставропольский край, г. Георгиевск";
                    officer_street = "ул. Калинина, 10";
                    break;
                case 5:
                    placement = "Кировского"; subjectRF = "Ставропольскому краю";
                    officer = "Т. С. Коробейниковой";
                    officer_address = "357300, Ставропольский край, г. Новопавловск";
                    officer_street = "ул. Мира, 190 Б";
                    break;
                case 6:
                    placement = "{{ВВЕДИТЕ РАЙОН}}";
                    subjectRF = "{{ВВЕДИТЕ СУБЪЕКТ РФ}}";
                    officer = "{{ВВЕДИТЕ ПРИСТАВА}}";
                    officer_address = "{{ВВЕДИТЕ АДРЕС СУДЕБНОГО ОТДЕЛЕНИЯ}}";
                    officer_street = "{{ВВЕДИТЕ УЛИЦУ СУДЕБНОГО ОТДЕЛЕНИЯ}}";
                    break;
            }

            string documentRef = data?.DocumentReference ?? "";

            string childInfo = string.IsNullOrEmpty(documentRef)
                ? genitiveCaseFullName.Trim()
                : genitiveCaseFullName.Trim() + ", " + documentRef;

            string parentInfo = parentFullName.Trim(); // ← НОВОЕ: имя представителя

            string debtors = "";
            if (genitiveCaseFullName.Contains(","))
                debtors = "должников";
            else
                debtors = "должника";

            var replacements = new Dictionary<string, string>
    {
        {"{{TODAYDATE}}", DateTime.Now.Date.ToString("dd.MM.yyyy")},
        {"{{SUBJECT_RF}}", subjectRF},
        {"{{AMOUNTSUM}}", amountSum.Trim()},
        {"{{c_DOCUMENTTYPE}}", c_documentType},
        {"{{i_DOCUMENTTYPE}}", i_documentType},
        {"{{LIVINGAT}}", livingAtString.Trim()},
        {"{{ORDERNUMBER}}", orderNumber.Trim()},
        {"{{CHILD_FULLNAME}}", childInfo},
        {"{{DEbtor}}", debtors},
        {"{{PARENT_FULLNAME}}", parentInfo}, // ← ИСПРАВЛЕНО: было ""
        {"{{ADDRESS}}", fullAddress.Trim()},
        {"{{PLACEMENT}}", placement},
        {"{{OFFICER}}", officer},
        {"{{COMPANYLOWER}}", lawyer},
        {"{{OFFICER_ADDRESS}}", officer_address},
        {"{{OFFICER_STREET}}", officer_street},
        {"{{APPENDIX}}", documentRef},
    };

            InitiateDocument(templatePath, outputPath, replacements);
        }

        private void InitiateDocument(string templatePath, string outputPath, Dictionary<string, string> replacements)
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            File.Copy(templatePath, outputPath, true);

            using (var doc = WordprocessingDocument.Open(outputPath, true))
            {
                var mainPart = doc.MainDocumentPart;
                var document = mainPart.Document;
                var paragraphs = document.Descendants<Paragraph>();

                foreach (var paragraph in paragraphs)
                {
                    var textNodes = paragraph.Descendants<Text>().ToList();
                    if (!textNodes.Any()) continue;

                    string fullText = string.Concat(textNodes.Select(t => t.Text));
                    bool replaced = false;

                    foreach (var kvp in replacements)
                    {
                        if (fullText.Contains(kvp.Key))
                        {
                            string newText = fullText.Replace(kvp.Key, kvp.Value);
                            textNodes[0].Text = newText;
                            for (int i = 1; i < textNodes.Count; i++)
                                textNodes[i].Text = "";
                            replaced = true;
                            break; // чтобы не ломать текст многократными заменами
                        }
                    }
                }
                doc.Save();
            }
        }
    }
}
