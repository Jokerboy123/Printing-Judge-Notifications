using System;
using System.Globalization;
using System.Windows;

namespace Printing_Judge_Notifications
{
    public static class RussianDeclension
    {
        private static bool IsNonDeclinableSuffix(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return false;
            string lawyer = word.ToLower(CultureInfo.InvariantCulture);
            return lawyer == "оглы" || lawyer == "кызы";
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
		/// <summary>
		/// Возвращает true, если владелец точно женский (по суффиксам/отчеству),
		/// false — если точно мужской, null — если неясно.
		/// </summary>
		public static bool? IsFemale(string patronymicWithSuffix)
		{
			if (string.IsNullOrWhiteSpace(patronymicWithSuffix))
				return null;

			string p = patronymicWithSuffix.ToLower().Trim();

			if (p.Contains("кызы")) return true;
			if (p.Contains("оглы")) return false;

			if (p.EndsWith("вна") || p.EndsWith("ична") || p.EndsWith("евна")) return true;
			if (p.EndsWith("вич") || p.EndsWith("ич")) return false;

			return null; // неясно
		}

		public static string GetLivingAt(string patronymicWithSuffix, int ownerCount)
		{
			// Если владельцев больше 1, нам нужно смотреть на всех, а не только на последнего.
			// Но здесь у нас только одна строка (последнего владельца).
			// Значит, эту логику надо перенести туда, где у нас есть весь список segments.
			// Поэтому для случая ownerCount > 1 этот метод лучше вообще не использовать.
			// Ниже — универсальная реализация, которая работает корректно и для 1 владельца.

			if (ownerCount == 0)
				return "проживающих по адресу: ";

			if (string.IsNullOrWhiteSpace(patronymicWithSuffix))
			{
				// Если нет данных для определения пола — безопаснее множественное
				return ownerCount > 1 ? "проживающих по адресу: " : "проживающего по адресу: ";
			}

			string p = patronymicWithSuffix.ToLower().Trim();

			// Сначала проверяем суффиксы «кызы/оглы» — они однозначны
			if (p.Contains("кызы"))
				return "проживающей по адресу: ";
			if (p.Contains("оглы"))
				return "проживающего по адресу: ";

			// Потом окончания отчеств — тоже однозначны
			if (p.EndsWith("вна") || p.EndsWith("ична") || p.EndsWith("евна"))
				return "проживающей по адресу: ";
			if (p.EndsWith("вич") || p.EndsWith("ич"))
				return "проживающего по адресу: ";

			// Если ничего не подошло — возвращаем по количеству
			// Это покрывает случаи, когда отчества нет, или оно нестандартное
			return ownerCount > 1 ? "проживающих по адресу: " : "проживающего по адресу: ";
		}

		private static string CapitalizeFirst(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (input.Length == 1) return char.ToUpper(input[0]).ToString();
            return char.ToUpper(input[0]) + input[1..].ToLower();
        }

        private static bool IsRussianConsonant(char c)
        {
            char lawyer = char.ToLower(c);
            return !"аеёиоуыэюя".Contains(lawyer);
        }
    }
}
