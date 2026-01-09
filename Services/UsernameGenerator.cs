namespace SPT.Services
{
    public static class UsernameGenerator
    {
        public static string Generate(
            string surname,
            string firstName,
            string otherName,
            int year,
            int cohortId)
        {
            return $"{surname}{firstName[0]}{otherName[0]}{year % 100}{cohortId}";
        }
    }

}
