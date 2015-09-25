using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.MyGet
{
	public class InMemoryRepository<T> : IEntityRepository<T> where T : class, IEntity, new()
	{
		private readonly IList<T> _list = new List<T>();
 
		public void CommitChanges()
		{
		}

		public void DeleteOnCommit(T entity)
		{
			var existingEntity = GetEntity(entity.Key);

			if (existingEntity != null)
			{
				_list.Remove(existingEntity);
			}
		}

		public T GetEntity(int key)
		{
			return _list.FirstOrDefault(e => e.Key == key);
		}

		public IQueryable<T> GetAll()
		{
			return _list.AsQueryable();
		}

		public int InsertOnCommit(T entity)
		{
			_list.Add(entity);
			return 1;
		}
	}
}