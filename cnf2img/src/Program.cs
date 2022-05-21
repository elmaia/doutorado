using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace cnf2img
{
  class Program
  {

    static void modoDeUso()
    {
      Console.Error.WriteLine(new String('-', 80));
      Console.Error.WriteLine("Modo de Uso:");
      Console.Error.WriteLine("{0} <arquivoCNF> [<arquivoBMP> <dimensao>]", System.Diagnostics.Process.GetCurrentProcess().MainModule.ModuleName);
      Console.Error.WriteLine(new String('-', 50));
      Console.Error.WriteLine("arquivoCNF:  Caminho do arquivo que contem a descricao do arquivo CNF-DIMACS");
      Console.Error.WriteLine("arquivoBMP:  Opcional. Caminho para o arquivo de saida da imagem do circuito.");
      Console.Error.WriteLine("             Se omitido, será gerado um arquivo chamado saida.bmp no diretorio atual.");
      Console.Error.WriteLine("             Nao adicione a extensao .bmp. Ela sera adicionada automaticamente!");
      Console.Error.WriteLine("dimensao..:  Opcional. Imagem redimensionada para um quadrado <dimensao>x<dimensao>");
      Console.Error.WriteLine(new String('-', 80));
    }

    static void Main(string[] args)
    {

      string nomeArquivoCNF;
      string nomeArquivoBMP = "saida.bmp";
      int dimensao = 0;

      // Verifica se os parâmetros
      if (args.Length > 0)
      {
        nomeArquivoCNF = String.Copy(args[0]);
      } else
      {
        modoDeUso();
        return;
      }
      if (args.Length > 1)
      {
        nomeArquivoBMP = String.Copy(args[1]);
      }
      if (args.Length > 2)
      {
        dimensao = int.Parse(args[2]);
      }

      // Cria uma representação CNF do arquivo
      Console.WriteLine("Lendo arquivo CNF...");
      CNF cnf = new CNF(nomeArquivoCNF);

      // Imprime o comentário do arquivo
      if (!String.IsNullOrEmpty(cnf.comment.ToString()))
      {
        Console.WriteLine(cnf.comment);
      }

      // Gera o BMP e salva no arquivo especificado
      Console.WriteLine("Gerando arquivo BMP...");
      Bitmap bmp = Bmp.generateFromCNF(cnf);
      bmp.Save(nomeArquivoBMP + ".original.bmp", ImageFormat.Bmp);

      // Gera o binário
      Console.WriteLine("Gerando arquivo BIN...");
      BinaryWriter bw = Bin.generateFromCNF(cnf, new FileStream(nomeArquivoBMP + ".bin", FileMode.Create));
      bw.Flush();
      bw.Close();

      if (dimensao != 0)
      {
        Console.WriteLine("Gerando arquivo BMP redimensionado...");
        Bitmap bmpResized = new Bitmap(bmp, new Size(dimensao, dimensao));
        bmpResized.Save(nomeArquivoBMP + ".scaled.bmp");

        Console.WriteLine("Gerando arquivo BIN redimensionado...");
        bw = Bin.genereteFromBitmap(bmpResized,new FileStream(nomeArquivoBMP + ".scaled.bin", FileMode.Create));

      }

      Console.WriteLine("Arquivos gerados com sucesso.");

    }
  }

  class Bin
  {
    public static BinaryWriter generateFromCNF(CNF cnf, FileStream fs)
    {
      BinaryWriter bw = new BinaryWriter(fs);

      // Percorre todos os bytes de todas as cláusulas e transforma em um pixel
      foreach (Clause c in cnf.lstClauses)
      {
        foreach (byte b in c.ascii)
        {
          bw.Write(b);
        }
        // Adiciona um \n
        bw.Write((byte)13);
      }

      // Se chegou até aqui é porque o binário foi gerado corretamente
      return bw;
    }

    public static BinaryWriter genereteFromBitmap(Bitmap bmp, FileStream fs)
    {
      BinaryWriter bw = new BinaryWriter(fs);

      for (int y = 0; y <  bmp.Height; y++)
      {
        for(int x = 0; x < bmp.Width; x++)
        {
          Color c = bmp.GetPixel(x, y);
          bw.Write((byte)(0.2989 * c.R + 0.5870 * c.G + 0.1140 * c.B));
        }
      }

      // Se chegou até aqui é porque o binário foi geado corretamete
      return bw;
    }
  }

  class Bmp
  {
    public static Bitmap generateFromCNF(CNF cnf)
    {
      int width = (int)Math.Ceiling(Math.Sqrt(cnf.NumBytes + cnf.lstClauses.Count));
      Bitmap bmp = new Bitmap(width, width);
      int col = 0;
      int row = 0;
      int grayScale;

      // Percorre todos os bytes de todas as cláusulas e transforma em um pixel
      foreach (Clause c in cnf.lstClauses)
      {
        foreach(byte b in c.ascii)
        {
          // Cria o pixel cinza equivalente ao valor do byte
          grayScale = (int)((b * 0.3) + (b * 0.59) + (b * 0.11));
          Color newCor = Color.FromArgb(255, grayScale, grayScale, grayScale);
          bmp.SetPixel(col++, row, newCor);

          // Verifica se já completou a largura... se sim, passa para próxima linha
          if (col == width)
          {
            col = 0;
            row++;
          }
        }

        // Adiciona um equivalente ao \n
        // Cria o pixel cinza equivalente ao valor do byte
        grayScale = (int)((13 * 0.3) + (13 * 0.59) + (13 * 0.11));
        bmp.SetPixel(col++, row, Color.FromArgb(255, grayScale, grayScale, grayScale));

        // Verifica se já completou a largura... se sim, passa para próxima linha
        if (col == width)
        {
          col = 0;
          row++;
        }

      }

      // Se chegou até aqui é porque o BMP foi gerado corretamente
      return bmp;
    }
  }

  /**
   * Classe responsável por representar um arquivo CNF como uma lista de cláusulas.
   */
  class CNF
  {

    #region members
    // Comentários
    public StringBuilder comment = new StringBuilder();
    // Número de variáveis
    public uint NumVariables { get; }
    // Número de cláusulas
    public uint NumClauses { get; }
    // Lista de cláusulas
    public List<Clause> lstClauses { get; }
    // Número de bytes de todas as cláusulas
    public long NumBytes { get; }
    #endregion

    private string readComment(string line)
    {
      return line.Substring(line.IndexOf("c", StringComparison.InvariantCultureIgnoreCase) + 1).TrimStart();
    }

    private struct cnfParameters {
      public string type;
      public uint nVars;
      public uint nClauses;

      public cnfParameters(string type, uint nVars, uint nClauses)
      {
        this.type = type;
        this.nVars = nVars;
        this.nClauses = nClauses;
      }
    }

    private cnfParameters readParameters(string line)
    {
      string[] parameters = line.Split(' ');
      return new cnfParameters(parameters[1], uint.Parse(parameters[2]), uint.Parse(parameters[3]));
    }

    public CNF(string filename)
    {

      // Lista das cláusulas lidas do arquivo CNF
      this.lstClauses = new List<Clause>();
      
      // Inicialmente, nenhum byte carregado
      this.NumBytes = 0;

      // Varre o arquivo
      using(StreamReader sr = new StreamReader(filename))
      {

        string line = "";
        while((line = sr.ReadLine()) != null)
        {

          // Verifica o tipo da linha
          switch(line.TrimStart()[0])
          {
            // Comentário
            case 'c':
              if (comment.Length != 0)
              {
                comment.Append("\n");
              }
              this.comment.Append(readComment(line));
              break;
            
            // Parâmetros
            case 'p':
              cnfParameters p = readParameters(line);
              this.NumVariables = p.nVars;
              this.NumClauses = p.nClauses;
              break;

            // Cláusula
            default:
              Clause c = new Clause(line);
              lstClauses.Add(c);
              this.NumBytes += c.ascii.Length;
              break;
          }
        }
      }
    }
  }

  /**
   * Classe responsável por representar uma cláusula CNF
   */
  class Clause
  {

    // Variáveis da cláusula
    public int[] vars { get; }
    public byte[] ascii { get; }

    /**
     * Construtor da cláusula
     * param name="clause" String representando a cláusula
     */
    public Clause(string clause)
    {

      // Para a transformação ingênua... cada caractere vira um byte
      this.ascii = new byte[clause.Length];
      int i = 0;
      foreach(char c in clause)
      {
        ascii[i++] = (byte)(c);
      }

      // Quebra a linha nos espaços
      string[] variables = clause.Split(' ');
      // Terminar com 0 é opcional, então a dimensão é -1 se terminar com 0
      this.vars = new int[variables.Length - ((variables.Last()[0] == '0') ? 1 : 0)];

      // Adiciona as variáveis na cláusula
      i = 0;
      foreach(string s in variables)
      {
        if (s.Equals("0")) break;
        this.vars[i++] = int.Parse(s.Trim());
      }
    }
  }
}
