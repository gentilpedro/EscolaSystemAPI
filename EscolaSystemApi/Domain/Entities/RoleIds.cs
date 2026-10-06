namespace EscolaSystemApi.Domain.Entities;

// Espelha os registros semeados em RoleConfiguration
public static class RoleIds
{
    public const int Admin = 1;
    public const int Director = 2;
    public const int Teacher = 3;
    public const int Student = 4;
    public const int Parent = 5;
    public const int Orientador = 6;

    // Perfis que o diretor gerencia dentro da escola
    public static readonly int[] SchoolMembers = [Teacher, Student, Parent, Orientador];

    // Perfis que podem estar em mais de uma escola ao mesmo tempo (diretor e aluno têm uma só)
    public static readonly int[] MultiSchool = [Teacher, Orientador, Parent];

    // Perfis que o administrador da plataforma gerencia
    public static readonly int[] Platform = [Admin, Director];
}
