using System;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using Ploeh.AutoFixture;

/// <summary>
/// These methods use AutoFixture and Entity Framework to populate database with data.
/// It was used as a quick way to populate a blank database under test without using
/// several helper methods.
/// AutoFixture: https://github.com/AutoFixture/AutoFixture
///     This can be found on Nuget as well.
/// </summary>
public class AutoPopulateDatabase
{
    protected readonly Fixture _fixture;

    public AutoPopulateDatabase()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.Remove(new ThrowingRecursionBehavior());
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }


    /// <summary>
    /// Returns an entity that is saved to the database.
    /// </summary>
    /// <typeparam name="T">the type of entity to be added to the context</typeparam>
    /// <param name="dbContext">The DbContext to save the entity to</param>
    /// <param name="setupAction">
    /// This allows for any modifications to the data before it is populated.
    /// Use this if certain values need to be set before being saved.
    /// </param>
    /// <returns>Returns the resultant entity that was saved to the DbContext</returns>
    /// <remarks>
    /// This will also clear out any virtual properties setting them to null.
    /// This is so navigation properties don't get populated.
    /// Usage would read as follows: 
    /// var myEntity = GivenEntity<EntityType1>(context); //stores a row in the EntityType1 table and returns it to be used
    /// var myEntityChild = GivenEntity<EntityType2>(context, et => et.Property1 = myEntity.Property1); 
    /// </remarks>
    protected T GivenEntity<T>(DbContext dbContext, Action<T> setupAction = null)
        where T : class, new()
    {
        var entity = _fixture.Create<T>();
        ClearNavigationProperties(entity);

        if (setupAction != null)
            setupAction(entity);

        var properties = dbContext.GetType().GetProperties();
        (from prop in properties where prop.PropertyType == typeof(DbSet<T>) select prop)
            .ToList()
            .ForEach(
                pi =>
                {
                    typeof(DbSet<T>)
                        .GetMethod("Add")
                        .Invoke(pi.GetValue(dbContext, null), new object[] { entity });
                }
            );

        dbContext.SaveChanges();
        return entity;
    }

    /// <summary>
    /// Clears out navigation properties that are set to virtual.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="entity"></param>
    /// <remarks>If the navigation property isn't null, pass in an action to set it to null.</remarks>
    private void ClearNavigationProperties<T>(T entity) where T : class
    {
        PropertyInfo[] properties = entity.GetType().GetProperties()
            .Where(p => p.GetGetMethod().IsVirtual).ToArray();

        properties.ToList().ForEach(pi =>
        {
            pi.SetValue(entity, null);
        });
    }
}
