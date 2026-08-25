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
        /// <summary>
        /// Извлекает вторую фамилию из скобок ТОЛЬКО из переданной строки.
        /// Возвращает очищенную строку и вторую фамилию отдельно.
        /// </summary>
        
        
        private static (string cleaned, string secondSurname) ExtractSecondSurnameFromSegment(string input)
        {
            string cleaned = input;
            string secondSurname = null;

            if (string.IsNullOrWhiteSpace(input))
                return ("", null);

            int open = input.IndexOf('(');
            if (open >= 0)
            {
                int close = input.IndexOf(')', open);
                if (close > open)
                {
                    secondSurname = input.Substring(open + 1, close - open - 1).Trim();
                    cleaned = (input.Remove(open, close - open + 1)).Trim();
                }
            }
            return (cleaned, secondSurname);
        }

        /// <summary>
        /// Разбивает строку ФИО на части: фамилия, имя, отчество (основа), суффикс.
        /// Гарантирует, что фамилия = первое слово, суффикс = последнее (если оглы/кызы).
        /// </summary>
        private static (string surname, string name, string patronymicBase, string suffix) ParseNameParts(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return ("", "", "", "");

            var parts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count == 0)
                return ("", "", "", "");

            string suffix = "";
            // Проверяем последнее слово на суффикс
            string lastWord = parts[^1].ToLower(CultureInfo.InvariantCulture);
            if (lastWord == "оглы" || lastWord == "кызы")
            {
                suffix = parts[^1]; // сохраняем регистр
                parts.RemoveAt(parts.Count - 1);
            }

            if (parts.Count == 0)
                return ("", "", "", suffix);

            string surname = parts[0];
            if (parts.Count < 2)
                return (surname, "", "", suffix);

            string name = parts[1];
            string patronymicBase = "";

            if (parts.Count > 2)
            {
                patronymicBase = string.Join(" ", parts.Skip(2));
            }

            return (surname, name, patronymicBase, suffix);
        }

        public void GenerateTemplate(string templatePath, string outputPath, OwnerData data, int selectedLower, int townNumber)
        {
            string AddPart(string current, string prefix, string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return current + prefix + value;
                return current;
            }

            // --- 1. АДРЕС ---
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

            string fullAddress = addressBase;
            fullAddress = AddPart(fullAddress, ", корп. ", data.Korps);
            fullAddress = AddPart(fullAddress, ", кв. ", data.Flat);
            fullAddress = AddPart(fullAddress, ", ком. ", data.Room);

            // --- 2. ПОДГОТОВКА ФИО (ИСПРАВЛЕННАЯ ЛОГИКА) ---
            string document = data?.DocumentReference ?? "";
            string fullName = data?.FullName ?? "";
            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.Show("ФИО не заполнено!");
                return;
            }

            fullName = fullName.Trim();

            string parentRaw = "";
            string childRaw = "";

            // ВАЖНО: Сначала определяем разделитель, чтобы разбить на сегменты
            // Ищем позицию "за" или "/"
            int separatorIndex = -1;
            bool isSlashSeparator = false;

            // Ищем "за" как отдельное слово
            var partsRaw = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            separatorIndex = partsRaw.FindIndex(p => p.Equals("за", StringComparison.OrdinalIgnoreCase));

            if (separatorIndex >= 0)
            {
                // "за" найден
                parentRaw = string.Join(" ", partsRaw.Take(separatorIndex));
                childRaw = string.Join(" ", partsRaw.Skip(separatorIndex + 1));
            }
            else if (fullName.Contains("/"))
            {
                // разбивка
                isSlashSeparator = true;
                var splitBySlash = fullName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                if (splitBySlash.Length >= 2)
                {
                    string[] owners = new string[splitBySlash.Length];

                    for (int i = 0; i < splitBySlash.Length; i++)
                    {
                        owners[i] = splitBySlash[i].Trim();
                        MessageBox.Show(owners[i]);
                    }
                }
                else
                {
                    // Если слэш есть, но частей мало — считаем всё ребёнком
                    childRaw = fullName;
                }
            }

            else
            {
                // Нет разделителей - считаем всё одним человеком (ребенком)
                childRaw = fullName;
            }

            // --- 3. ОБРАБОТКА СЕГМЕНТОВ (КАЖДЫЙ ОТДЕЛЬНО) ---
            
            // Обработка ребенка
            var (childClean, childSecondSurnameRaw) = ExtractSecondSurnameFromSegment(childRaw);
            var childParts = ParseNameParts(childClean);
            
            string childSurname = childParts.surname;
            string childName = childParts.name;
            string childPatronymicBase = childParts.patronymicBase;
            string childSuffix = childParts.suffix;
            string childSecondSurname = !string.IsNullOrEmpty(childSecondSurnameRaw) ? childSecondSurnameRaw : "";

            // Обработка отца (если есть)
            string parentSurname = "";
            string parentName = "";
            string parentPatronymicBase = "";
            string parentSuffix = "";
            string parentSecondSurname = "";

            if (!string.IsNullOrEmpty(parentRaw))
            {
                var (parentClean, parentSecondSurnameRaw) = ExtractSecondSurnameFromSegment(parentRaw);
                var pParts = ParseNameParts(parentClean);

                parentSurname = pParts.surname;
                parentName = pParts.name;
                parentPatronymicBase = pParts.patronymicBase;
                parentSuffix = pParts.suffix;
                parentSecondSurname = !string.IsNullOrEmpty(parentSecondSurnameRaw) ? parentSecondSurnameRaw : "";
            }

            // --- 4. СКЛОНЕНИЕ (только основы) ---
            string DeclineSafeSurname(string s) => !string.IsNullOrWhiteSpace(s) ? RussianDeclension.DeclineSurname(s) : s;
            string DeclineSafeName(string n)   => !string.IsNullOrWhiteSpace(n) ? RussianDeclension.DeclineName(n) : n;
            string DeclineSafePatronymic(string p) => !string.IsNullOrWhiteSpace(p) ? RussianDeclension.DeclinePatronymic(p) : p;

            // Сын
            string declinedChildSurname = DeclineSafeSurname(childSurname);
            string declinedChildName = DeclineSafeName(childName);
            string declinedChildPatronymic = DeclineSafePatronymic(childPatronymicBase);
            string declinedChildSecondSurname = !string.IsNullOrEmpty(childSecondSurname)
                ? DeclineSafeSurname(childSecondSurname) : "";

            // Отец
            string declinedParentSurname = "";
            string declinedParentName = "";
            string declinedParentPatronymic = "";
            string declinedParentSecondSurname = "";

            if (!string.IsNullOrEmpty(parentSurname))
            {
                declinedParentSurname = DeclineSafeSurname(parentSurname);
                declinedParentName = DeclineSafeName(parentName);
                declinedParentPatronymic = DeclineSafePatronymic(parentPatronymicBase);

                if (!string.IsNullOrEmpty(parentSecondSurname))
                    declinedParentSecondSurname = DeclineSafeSurname(parentSecondSurname);
            }

            // Сборка ФИО
            string BuildFullName(
                string surname, string secondSurname, string name, string patrBase, string suffix)
            {
                var partsList = new List<string>();

                string finalSurname = surname;
                if (!string.IsNullOrEmpty(secondSurname))
                    finalSurname = $"{surname} ({secondSurname})";

                if (!string.IsNullOrEmpty(finalSurname)) partsList.Add(finalSurname);
                if (!string.IsNullOrEmpty(name)) partsList.Add(name);
                if (!string.IsNullOrEmpty(patrBase)) partsList.Add(patrBase);
                if (!string.IsNullOrEmpty(suffix)) partsList.Add(suffix);
                return string.Join(" ", partsList);
            }

            string genitiveCaseChildFullName = BuildFullName(
                declinedChildSurname, declinedChildSecondSurname,
                declinedChildName, declinedChildPatronymic, childSuffix);

            string genitiveCaseParentFullName = "";
            if (!string.IsNullOrEmpty(declinedParentSurname))
            {
                genitiveCaseParentFullName = BuildFullName(
                    declinedParentSurname, declinedParentSecondSurname,
                    declinedParentName, declinedParentPatronymic, parentSuffix);
                genitiveCaseParentFullName =  Regex.Replace(genitiveCaseParentFullName, @"(,\s*)([a-zа-яё])",
    m => m.Groups[1].Value + m.Groups[2].Value.ToUpper());
            }

            string childFullNameForGenderCheck = (childPatronymicBase + " " + childSuffix).Trim();

            
            // Префикс «проживающего/проживающей»
            string livingAtString = RussianDeclension.GetLivingAt(childFullNameForGenderCheck);

          
            var orderNumber = data.OrderNumber.ToString();

            string c_documentType = "";

            if (orderNumber.Contains("ВС") || orderNumber.Contains("BC"))
            {
                c_documentType = "исполнительному листу №";
            }
            else
            {
                c_documentType = "судебному приказу №";
            }

            string i_documentType = "";

            if (orderNumber.Contains("ВС") || orderNumber.Contains("BC"))
            {
                i_documentType = "исполнительного листа №";
            }
            else
            {
                i_documentType = "судебного приказа №";
            }


            string amountSum = data.AmountSum.ToString();

            string placement = "";
            string subjectRF = "";
            string officer = "";
            string officer_address = "";
            string officer_street = "";
            string lawyer = "";
            string documentReference = "";

            switch (selectedLower)
            {
                case 1:
                    lawyer = "C. В. Ловянников";
                    break;
                case 2:
                    lawyer = "О. А. Король";
                    break;
            }

            switch (townNumber)
            {
                case 1:
                    placement = "Курского";
                    subjectRF = "Cтавропольскому краю";
                    officer = "А. И. Заргарову";
                    officer_address = "357850, Ставропольский край, ст. Курская";
                    officer_street = "пр. Комсомольский, 8";
                    break;
                case 2:
                    placement = "Степновского";
                    subjectRF = "Cтавропольскому краю";
                    officer = "О. В. Балдовой";
                    officer_address = "357930, Ставропольский край, с. Степное";
                    officer_street = "пл. Ленина, 17а";
                    break;
                case 3:
                    placement = "Советского";
                    subjectRF = "Cтавропольскому краю";
                    officer = "А. Г. Ржевскому";
                    officer_address = "357914, Ставропольский край, г. Зеленокумск";
                    officer_street = "ул. 50 лет Октября, 51";
                    break;
                case 4:
                    placement = "Георгиевского";
                    subjectRF = "Cтавропольскому краю";
                    officer = "А. П. Капуста";
                    officer_address = "357820, Ставропольский край, г. Георгиевск";
                    officer_street = "ул. Калинина, 10";
                    break;
                case 5:
                    placement = "Кировского";
                    subjectRF = "Cтавропольскому краю";
                    officer = "Т. С. Коробейниковой";
                    officer_address = "357300, Ставропольский край, г. Новопавловск";
                    officer_street = "ул. Мира, 190 Б";
                    break;
                case 6:
                    placement = "{{ВВЕДИТЕ РАЙОН}}";
                    officer = "{{ВВЕДИТЕ ПРИСТАВА}}";
                    officer_address = "{{ВВЕДИТЕ АДРЕС СУДЕБНОГО ОТДЕЛЕНИЯ}}";
                    officer_street = "{{ВВЕДИТЕ УЛИЦУ СУДЕБНОГО ОТДЕЛЕНИЯ}}";
                    subjectRF = "{{ВВЕДИТЕ СУБЪЕКТ РФ}}";
                    
                    break;
                default:
                    break;
            }



            string childInfo = "";
            if (document.ToString() == "")
           
                childInfo = genitiveCaseChildFullName.Trim();
            else 
                childInfo = genitiveCaseChildFullName.Trim() + ", " + document.ToString();

            var replacements = new Dictionary<string, string>
            {
                {"{{TODAYDATE}}", DateTime.Now.Date.ToString("dd.MM.yyyy")},
                {"{{SUBJECT_RF}}", subjectRF.ToString()},
                {"{{AMOUNTSUM}}", amountSum.Trim() ?? ""},
                {"{{c_DOCUMENTTYPE}}", c_documentType.Trim() ?? ""},
                {"{{i_DOCUMENTTYPE}}", i_documentType.Trim() ?? "" },
                {"{{LIVINGAT}}", livingAtString.Trim() ?? ""},
                {"{{ORDERNUMBER}}", orderNumber.Trim() ?? ""},
                {"{{CHILD_FULLNAME}}", childInfo ?? ""},
                {"{{PARENT_FULLNAME}}", !string.IsNullOrEmpty(genitiveCaseParentFullName) ? genitiveCaseParentFullName.Trim() : ""},
                {"{{ADDRESS}}", fullAddress.Trim()},
                {"{{PLACEMENT}}", placement },
                {"{{OFFICER}}", officer},
                {"{{COMPANYLOWER}}", lawyer},
                {"{{OFFICER_ADDRESS}}", officer_address},
                {"{{OFFICER_STREET}}", officer_street},
                {"{{APPENDIX}}", documentReference},
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

                    foreach (var kvp in replacements)
                    {
                        if (fullText.Contains(kvp.Key))
                        {
                            string newText = fullText.Replace(kvp.Key, kvp.Value);
                            textNodes[0].Text = newText;
                            for (int i = 1; i < textNodes.Count; i++)
                            {
                                textNodes[i].Text = "";
                            }
                        }
                    }
                }
                doc.Save();
            }
        }
    }
}
