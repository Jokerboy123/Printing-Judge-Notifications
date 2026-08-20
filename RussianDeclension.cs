using System;
using System.Globalization;

namespace Printing_Judge_Notifications
{
    public static class RussianDeclension
    {
        private static bool IsNonDeclinableSuffix(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return false;
            string lower = word.ToLower(CultureInfo.InvariantCulture);
            return lower == "оглы" || lower == "кызы";
        }

        public static string DeclineSurname(string surname)
        {
            if (string.IsNullOrWhiteSpace(surname)) return surname;
            surname = surname.Trim();
            if (surname.Length < 2) return surname;

            if (IsNonDeclinableSuffix(surname))
                return surname;

            char lastChar = surname[^1];
            string last2 = surname[^2..];
            string baseStr = surname[..^2];

            // Исключения: несклоняемые окончания
            if (last2 == "ук" || last2 == "ян" || surname[^3..] == "янц" || surname[^3..] == "дзе")
                return surname;

            string result;

            switch (last2)
            {
                case "ов":
                case "ев":
                case "ин":
                case "ын":
                    result = surname + "а";
                    break;
                case "ый":
                case "ой":
                case "ий":
                    result = baseStr + "ого";
                    break;
                case "ая":
                    result = surname[..^2] + "ой";
                    break;
                case "яя":
                    result = surname[..^2] + "ей";
                    break;
                case "на":
                    char beforeNa = baseStr[^1];
                    if (beforeNa == 'и' || beforeNa == 'ы')
                        result = baseStr + "ной";
                    else
                        result = surname;
                    break;
                case "ва":
                    result = baseStr + "вой";
                    break;
                default:
                    result = IsRussianConsonant(lastChar) ? surname + "а" : surname;
                    break;
            }

            if (result.ToLower() == "хорошого")
                result = "хорошего";

            return CapitalizeFirst(result);
        }

        public static string DeclineName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            name = name.Trim();

            if (name.Contains("вич", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("вна", StringComparison.OrdinalIgnoreCase))
            {
                return DeclinePatronymic(name);
            }

            if (name.Length == 0) return name;

            char last1 = name[^1];
            string baseStr = name[..^1];
            string result;

            switch (last1)
            {
                case 'й':
                case 'ь':
                    result = baseStr + "я";
                    break;
                case 'а':
                    result = (!string.IsNullOrEmpty(baseStr) && baseStr[^1] != 'и') ? baseStr + "ы" : baseStr + "и";
                    break;
                case 'я':
                    result = baseStr + "и";
                    break;
                default:
                    string lowerName = name.ToLower();
                    switch (lowerName)
                    {
                        case "лев":
                        case "сергей":
                        case "алексей":
                        case "дмитрий":
                        case "андрей":
                        case "евгений":
                        case "игорь":
                        case "олег":
                            result = baseStr + "я";
                            break;
                        default:
                            result = IsRussianConsonant(last1) ? name + "а" : name;
                            break;
                    }
                    break;
            }

            if (result.Length >= 2 && result[^2..] == "аа")
                result = result[..^1];

            string lowerResult = result.ToLower();
            result = lowerResult switch
            {
                "иванаа" => "Ивана",
                "петраа" => "Петра",
                "владимираа" => "Владимира",
                "михаилаа" => "Михаила",
                "никитаа" => "Никиты",
                "сашаа" => "Саши",
                "дашаа" => "Даши",
                "машаа" => "Маши",
                "ольгы" => "Ольги",
                "лея" => "Льва",
                "олея" => "Олега",
                _ => result
            };

            return CapitalizeFirst(result);
        }

        /// <summary>
        /// Сюда должна попадать ТОЛЬКО основа отчества БЕЗ «оглы»/«кызы».
        /// </summary>
        public static string DeclinePatronymic(string patronymic)
        {
            if (string.IsNullOrWhiteSpace(patronymic)) return patronymic;
            patronymic = patronymic.Trim();

            if (IsNonDeclinableSuffix(patronymic))
                return patronymic;

            if (patronymic.Length < 2) return patronymic;

            string p = patronymic.ToLower();

            if (p.EndsWith("ович"))
                return CapitalizeFirst(patronymic[..^4] + "овича");
            if (p.EndsWith("евич"))
                return CapitalizeFirst(patronymic[..^4] + "евича");
            if (p.EndsWith("овна"))
                return CapitalizeFirst(patronymic[..^4] + "овны");
            if (p.EndsWith("евна"))
                return CapitalizeFirst(patronymic[..^4] + "евны");
            if (p.EndsWith("ична"))
                return CapitalizeFirst(patronymic[..^4] + "ичны");

            return patronymic; // Если не распознано — не ломаем
        }

        public static string GetLivingAt(string patronymicWithSuffix)
        {
            if (string.IsNullOrWhiteSpace(patronymicWithSuffix))
                return "проживающего по адресу: ";

            string p = patronymicWithSuffix.ToLower().Trim();

            if (p.Contains("кызы"))
                return "проживающей по адресу: ";
            if (p.Contains("оглы"))
                return "проживающего по адресу: ";

            if (p.EndsWith("вна") || p.EndsWith("ична") || p.EndsWith("евна"))
                return "проживающей по адресу: ";

            return "проживающего по адресу: ";
        }

        private static string CapitalizeFirst(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (input.Length == 1) return char.ToUpper(input[0]).ToString();
            return char.ToUpper(input[0]) + input[1..].ToLower();
        }

        private static bool IsRussianConsonant(char c)
        {
            char lower = char.ToLower(c);
            return !"аеёиоуыэюя".Contains(lower);
        }
    }
}
