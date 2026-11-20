namespace Domain.Const
{
    public static class UserSearchConst
    {
        public const int DefaultPageSize = 20;

        public static readonly string[] SortOptions =
        [
            "name_asc",
            "name_desc",
            "newest",
            "oldest"
        ];
    }
}
