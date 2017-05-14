using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Dynamic;
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



        public string toJson()
        {
            string json = string.Empty;
            using (SqlConnection sqlConnection = new SqlConnection(_connectionString))
            {
                sqlCommand.Connection = sqlConnection;
                sqlConnection.Open();
                var dr = sqlCommand.ExecuteReader();
                json = SerializeJson(dr);
            }
            return json;
        }

        protected string SerializeJson(SqlDataReader dr)
        {
            var retorno = new List<Dictionary<string, object>>();

            var dic = new Dictionary<string, object>();
            object ID = -1;

            while (dr.Read())
            {
                dic = addToDic(dr, dic, 0, ref ID, string.Empty);
                if (!retorno.Contains(dic))
                    retorno.Add(dic);
            }

            return JsonConvert.SerializeObject(retorno);

        }

        //protected Dictionary<string, object> SerializeToDictionary(SqlDataReader dr)
        //{

        //    var d = new Dictionary<string, object>();

        //    for (var i = 0; i < dr.FieldCount; i++)
        //    {
        //        object ID = null;
        //        AddToDic(dr.GetName(i), dr[i], ref d, null, ID);

        //        //d.Add(dr.GetName(i), dr[i]);
        //    }



        //    return d;

        //}


        //protected Dictionary<string, object> addToDic(SqlDataReader dr, Dictionary<string, object> d, int fieldIndex, ref object ID, string fieldNameList = "")
        //{

        //    Dictionary<string, object> dicToValue;

        //    dicToValue = d;
        //    if (ID != null && !String.IsNullOrEmpty(fieldNameList))
        //    {
        //        if (ID.ToString() != dr[fieldIndex].ToString())
        //        {
        //            var list = (List<Dictionary<string, object>>)d[fieldNameList];
        //            list.Add(new Dictionary<string, object>());
        //            dicToValue = list.First();
        //        }
        //        else
        //        {
        //            var list = (List<Dictionary<string, object>>)d[fieldNameList];
        //            dicToValue = list.First();
        //        }
        //    }


        //    for (var i = fieldIndex; i < dr.FieldCount; i++)
        //    {

        //        var f = dr.GetName(i);

        //        if (!string.IsNullOrEmpty(fieldNameList))
        //            f = Helper.RemoveFirst(dr.GetName(i), fieldNameList + '_');


        //        if (f.IndexOf("_") != -1)
        //        {
        //            var fs = f.Split('_');

        //            var list = new List<Dictionary<string, object>>();

        //            if (!dicToValue.ContainsKey(fs.First()))
        //                dicToValue.Add(fs.First(), list);

        //            ID = dr[fieldIndex];

        //            addToDic(dr, dicToValue, i, ref ID, fs.First());
        //            return dicToValue;
        //        }

        //        if (!dicToValue.ContainsKey(dr.GetName(i)))
        //            dicToValue.Add(dr.GetName(i), dr[i]);
        //    }

        //    return dicToValue;

        //}



        //protected void AddToDic(string fieldName, object value, ref Dictionary<string, object> d, string fieldNameParent = null, object ID = null)
        //{

        //    if (fieldName.IndexOf("_") == -1)
        //    {
        //        // simple
        //        if (d.ContainsKey(fieldName)) return;
        //        d.Add(fieldName, value);

        //    }
        //    else
        //    {

        //        // complex
        //        var fieldNames = fieldName.Split('_');

        //        Dictionary<string, object> dToValue = null;

        //        // Create structure
        //        for (var i = 0; i < fieldNames.Count(); i++)
        //        {
        //            if (i == fieldNames.Count() - 1)
        //            {
        //                // last
        //                dToValue.Add(fieldNames[i], value);
        //            }
        //            else
        //            {
        //                // Add all property dic to dictonary
        //                if (!d.ContainsKey(fieldNames[i]))
        //                    d.Add(fieldNames[i], new Dictionary<string, object>());

        //                dToValue = (Dictionary<string, object>)d[fieldNames[i]];
        //            }

        //        }

        //    }

        //}




        protected Dictionary<string, object> addToDic(SqlDataReader dr, Dictionary<string, object> d, int fieldIndex, ref object ID, string fieldNameList = "", int fieldIndexID = 0)
        {

            Dictionary<string, object> dicToValue;

            var IDNow = dr[fieldIndexID];

            if (ID.Equals(IDNow))
            {
                dicToValue = d;
            }
            else
            {
                dicToValue = new Dictionary<string, object>();
            }

            ID = IDNow;

            Dictionary<string, object> ret = null;
            object retID;
            for (var i = fieldIndex; i < dr.FieldCount; i++)
            {

                var f = dr.GetName(i);

                if (!string.IsNullOrEmpty(fieldNameList))
                    f = Helper.RemoveFirst(dr.GetName(i), fieldNameList + '_');

                if (f.IndexOf("_") != -1)
                {
                    var fs = f.Split('_');
                    var listFielsName = fs.First();

                    List<Dictionary<string, object>> list;
                    if (!dicToValue.ContainsKey(listFielsName))
                    {
                        list = new List<Dictionary<string, object>>();
                        dicToValue.Add(listFielsName, list);


                    }
                    else
                    {
                        list = (List<Dictionary<string, object>>)dicToValue[listFielsName];
                    }

                    retID = IDNow;

                    if (list.Any())
                    {
                        foreach (var item in list)
                        {
                            var retFieldName = Helper.UltimoField(dr.GetName(i));
                            if (item.ContainsKey(retFieldName) && item[retFieldName].Equals(dr[i]))
                            {
                                ret = item;
                                break;
                            }
                        }
                    }

                    if (ret == null)
                        ret = new Dictionary<string, object>();

                    ret = addToDic(dr, ret, i, ref retID, listFielsName, fieldIndex);
                    if (!list.Contains(ret))
                        list.Add(ret);


                    break;
                }

                if (!dicToValue.ContainsKey(Helper.UltimoField(dr.GetName(i))))
                    dicToValue.Add(Helper.UltimoField(dr.GetName(i)), dr[i]);
            }

            return dicToValue;

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

    internal class MyBag : DynamicObject
    {
        private readonly Dictionary<string, dynamic> _properties = new Dictionary<string, dynamic>(StringComparer.InvariantCultureIgnoreCase);

        public override bool TryGetMember(GetMemberBinder binder, out dynamic result)
        {
            result = this._properties.ContainsKey(binder.Name) ? this._properties[binder.Name] : null;

            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, dynamic value)
        {
            if (value == null)
            {
                if (_properties.ContainsKey(binder.Name))
                    _properties.Remove(binder.Name);
            }
            else
                _properties[binder.Name] = value;

            return true;
        }
    }


    internal static class Helper
    {

        internal static string RemoveFirst(string str, string removeString)
        {
            return str.Remove(0, str.IndexOf(removeString) + removeString.Length);
        }

        internal static string UltimoField(string str)
        {
            return str.Remove(0, str.LastIndexOf("_") + 1);
        }




    }

}
