using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogicalMinds.LMFrameworkEntity.UnitTest
{
    [TestClass]
    public class JsonTest
    {

        private LMFrameworkEntity.Entity.Entity _MyEntity;


        private LMFrameworkEntity.Entity.Entity MyEntity
        {
            get
            {
                if (_MyEntity == null) _MyEntity = new Entity.Entity(@"Data Source=LUCY6\SQLSERVER;Initial Catalog=RepomFrete;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Connect Timeout=60;Encrypt=False;TrustServerCertificate=True");

                return _MyEntity;
            }
        }


        [TestMethod]
        public void TestMethod1()
        {

            string json = MyEntity.Text(@"
SELECT
	c.ID as ClienteID
	,c.RazaoSocial AS ClienteRazaoSocial
	,p.ID AS Prestadores_ID
	,p.NomeFantasia AS Prestadores_NomeFantasia
	,v.ID AS Prestadores_Veiculos_ID
	,v.Placa AS Prestadores_Veiculos_Placa
FROM Cliente c
INNER JOIN vw_Prestador p ON
	p.ClienteID=c.ID
INNER JOIN Veiculo v ON
	v.VeiculoProprietarioID=p.ID
WHERE c.ID IN (10,8135,8135)
ORDER BY c.ID,p.ID

").toJson();

            Assert.IsNotNull(json);
            Console.WriteLine(json);


        }
    }
}
