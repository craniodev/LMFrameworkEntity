using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LogicalMinds.LMFrameworkEntity.Entity
{
    public class Entity
    {
        //private const string CONNECTION_STRING = @"Data Source=LUCY6\SQLSERVER;Initial Catalog=LogicalMindsQuiz;Integrated Security=True";
        //private const string CONNECTION_STRING = @"Data Source=iris.arvixe.com;Initial Catalog=LogicalMinds_Quiz;Persist Security Info=True;User ID=LogicalMinds;Password=logicaldb@2014";
        private readonly string _connectionString;
        private SqlCommand sqlCommand = new SqlCommand();

        public Entity(string connectionString)
        {
            //string CONNECTION_STRING = @"Data Source=191.232.184.229;Initial Catalog=Freto;Persist Security Info=True;User ID=FretoUser;Password=Freto0417@";
            //connectionString = CONNECTION_STRING;
            // _connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ToString();
            _connectionString = connectionString;
        }

        /// <summary>
        /// Cria um objeto a partir de um SqlDataReader.
        /// </summary>
        /// <typeparam name="T">Tipo do objeto a ser criado.</typeparam>
        /// <param name="dr">SqlDataReader</param>
        /// <returns>Retorna um objeto do tipo especificado carregado com as informações do SqlDataReader</returns>
        /// <remarks>O SqlDataReader deve ser aberto antes de ser passado e fechado após o uso.
        /// As propriedades do objeto são lincadas aos Itens do SqlDataReader para o preenchimento. 
        /// A query deve retornar os campos com nomes e tipos identicos aos do objeto.
        /// </remarks>        
        protected T SerializeEntities<T>(SqlDataReader dr)
        {
            return (T)SerializeEntities(dr, typeof(T));
        }

        protected object SerializeEntities(SqlDataReader dr, Type type)
        {
            var cols = new List<string>();
            for (int i = 0; i < dr.FieldCount; i++)
                cols.Add(dr.GetName(i));

            object objItem = Activator.CreateInstance(type);
            object value = null;

            foreach (var property in objItem.GetType().GetProperties())
            {
                var col = cols.FirstOrDefault(c => c == property.Name);
                switch (Type.GetTypeCode(property.PropertyType))
                {
                    case TypeCode.Decimal:
                    case TypeCode.Double:
                        if (string.IsNullOrEmpty(col) || ReferenceEquals(dr[col], DBNull.Value))
                            value = 0m;
                        else
                            value = dr[col];
                        break;
                    case TypeCode.Byte:
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                        if (string.IsNullOrEmpty(col) || ReferenceEquals(dr[col], DBNull.Value))
                            value = 0;
                        else
                            value = dr[col];
                        break;
                    case TypeCode.DateTime:
                        if (string.IsNullOrEmpty(col) || ReferenceEquals(dr[col], DBNull.Value))
                            value = DateTime.MinValue;
                        else
                            value = dr[col];
                        break;
                    case TypeCode.String:
                        if (string.IsNullOrEmpty(col) || ReferenceEquals(dr[col], DBNull.Value))
                            value = string.Empty;
                        else
                            value = dr[col];
                        break;
                    case TypeCode.Char:
                        if (string.IsNullOrEmpty(col) || ReferenceEquals(dr[col], DBNull.Value))
                            value = Convert.ToChar(value.ToString()[0]);
                        else
                            value = dr[col];
                        break;
                    case TypeCode.Boolean:
                        if (string.IsNullOrEmpty(col) || ReferenceEquals(dr[col], DBNull.Value))
                            value = Convert.ToBoolean(Convert.ToInt16(value.ToString()[0]));
                        else
                            value = dr[col];
                        break;
                    default:
                        var attr = GetAtribute(property);
                        if (attr == null)
                            value = null;
                        else
                        {
                            try
                            {
                                var myEntity = new Entity(_connectionString);
                                if (!string.IsNullOrEmpty(attr.Procedure))
                                    myEntity.StoredProcedure(attr.Procedure);
                                if (!string.IsNullOrEmpty(attr.Text))
                                    myEntity.Text(attr.Text);
                                if (!string.IsNullOrEmpty(attr.PropertyID))
                                {
                                    var parans = attr.ParamName.Split(',');
                                    var props = attr.PropertyID.Split(',');

                                    for (var i = 0; i < parans.Length; i++)
                                    {
                                        var param = parans[i];
                                        var prop = props[i];

                                        if (ReferenceEquals(dr[prop], DBNull.Value))
                                            throw new Exception("PropertyID " + attr.PropertyID + " não possui field de referencia");

                                        myEntity.AddWithValue("@" + param.Replace("@", string.Empty), dr[props[i].Replace("@", string.Empty)]);
                                    }
                                }

                                if (property.GetType().IsGenericType)
                                    throw new NotImplementedException();
                                else
                                    value = myEntity.Single(property.PropertyType);
                            }
                            catch (Exception ex)
                            {
                                throw new Exception(property.Name + " Erro ao carregar FieldEntityAttribute: " + ex.Message);
                            }
                        }
                        break;
                }
                property.SetValue(objItem, value, null);
            }

            return objItem;
        }

        private static FieldEntityAttribute GetAtribute(PropertyInfo prop)
        {
            var attrs = prop.GetCustomAttributes(true);
            foreach (var attr in attrs)
            {
                var authAttr = attr as FieldEntityAttribute;
                if (authAttr != null)
                    return authAttr;
            }
            return null;
        }

        public Entity StoredProcedure(string val)
        {
            sqlCommand.Parameters.Clear();
            sqlCommand.CommandType = CommandType.StoredProcedure;
            sqlCommand.CommandText = val;

            return this;
        }

        public Entity Text(string val)
        {
            sqlCommand.Parameters.Clear();
            sqlCommand.CommandType = CommandType.Text;
            sqlCommand.CommandText = val;

            return this;
        }

        public Entity AddWithValue(string parameterName, object value, bool IsOutput = false)
        {
            if (parameterName.IndexOf("@") == -1)
                parameterName = string.Concat("@", parameterName);
            if (value != null)
                this.sqlCommand.Parameters.AddWithValue(parameterName, value);
            else
                this.sqlCommand.Parameters.AddWithValue(parameterName, DBNull.Value);
            if (IsOutput)
            {
                this.sqlCommand.Parameters[parameterName].Direction = ParameterDirection.Output;
                this.sqlCommand.Parameters[parameterName].Size = 100;
            }
            return this;
        }

        public Entity Clear()
        {
            sqlCommand.Parameters.Clear();
            return this;
        }

        public T Single<T>() where T : new()
        {
            T obj = default(T);
            using (SqlConnection sqlConnection = new SqlConnection(_connectionString))
            {
                sqlCommand.Connection = sqlConnection;
                sqlConnection.Open();
                var dr = sqlCommand.ExecuteReader();
                if (dr.Read())
                    obj = SerializeEntities<T>(dr);
            }
            return obj;
        }

        public object Single(Type type)
        {
            object obj = null;
            using (SqlConnection sqlConnection = new SqlConnection(_connectionString))
            {
                sqlCommand.Connection = sqlConnection;
                sqlConnection.Open();
                var dr = sqlCommand.ExecuteReader();
                if (dr.Read())
                    obj = SerializeEntities(dr, type);
            }
            return obj;
        }

        public List<T> ToList<T>() where T : new()
        {
            var lst = new List<T>();
            using (SqlConnection sqlConnection = new SqlConnection(_connectionString))
            {

                sqlCommand.Connection = sqlConnection;
                sqlConnection.Open();
                var dr = sqlCommand.ExecuteReader();
                while (dr.Read())
                {
                    lst.Add(SerializeEntities<T>(dr));
                }
            }
            return lst;
        }

        public int ExecuteNonQuery()
        {
            var retorno = 0;
            using (SqlConnection sqlConnection = new SqlConnection(_connectionString))
            {
                sqlCommand.Connection = sqlConnection;
                sqlConnection.Open();
                retorno = sqlCommand.ExecuteNonQuery();
            }
            return retorno;
        }

        public object ExecuteScalar()
        {
            object retorno = null;
            using (SqlConnection sqlConnection = new SqlConnection(_connectionString))
            {
                sqlCommand.Connection = sqlConnection;
                sqlConnection.Open();
                retorno = sqlCommand.ExecuteScalar();
                for (int i = 0; i < sqlCommand.Parameters.Count; i++)
                {
                    if (sqlCommand.Parameters[i].Direction == ParameterDirection.Output)
                        retorno = sqlCommand.Parameters[i].Value;
                }
            }
            return retorno;
        }
    }

    internal class FieldEntityAttribute : Attribute
    {
        public string Procedure { get; set; }
        public string Text { get; set; }
        public string Paramter { get; set; }
        public string PropertyID { get; set; }
        public string ParamName { get; set; }
    }
}
