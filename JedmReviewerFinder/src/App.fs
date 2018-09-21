module App

open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Elmish
open Elmish.React
open Fable.Helpers.React
open Fable.Helpers.React.Props


// MODEL
type [<Pojo>] SearchResult =
    {
        Keyphrase: string
        Author : string
        Score : float
        Title : string //The purpose of returning the title is to document to provenance of the result
    }

type Model = 
    {
        Query : string
        Results : ResizeArray<SearchResult>
    }

type Msg =
| DoQuery
| UpdateQuery of string

let init() : Model = {Query=""; Results= new ResizeArray<SearchResult>() }


// UPDATE
//---------------------------------------------------------
// Utility functions for query; could be moved to own file
//---------------------------------------------------------
//Note we use hackish JS interop of making various functions
//global, see jupyter notebook for these shared types
//These have 1 letter properties b/c we minified the JSON
type IdScore =
    {
        I: int
        S: int
    }
type IdTitle =
    {
        I: int
        T: string
    }
type KeyScore =
    {
        K: string
        S: int
    }
type AuthorOrder =
    {
        A: string
        O: int
    }
type KeyIdScore =
    {
        K: string
        I: IdScore[]
    }
type IdAuthorOrder =
    {
        I: int
        A: AuthorOrder[]
    }
 

//Get a map from text id to a list of associated author orders
[<Import("idAuthorOrder","./idAuthorOrder.js")>]
let GetIdAuthorOrder: unit -> IdAuthorOrder[] = jsNative
let idAuthorOrder = 
    GetIdAuthorOrder()
    |> Seq.map( fun iao -> iao.I, iao.A)
    |> Map.ofSeq

//Get a map from text id to titles
[<Import("idTitle","./idTitle.js")>]
let GetIdTitle: unit -> string[] = jsNative
let idTitle = 
    GetIdTitle()
    |> Seq.mapi( fun i title -> i, title)
    |> Map.ofSeq

//Get a map from key phrases to text ids and associated scores
[<Import("keyIdScore","./keyIdScore.js")>]
let GetKeyIdScore: unit -> KeyIdScore[] = jsNative
let keyIdScore = 
    GetKeyIdScore()
    |> Seq.map( fun kis -> kis.K, kis.I)
    |> Map.ofSeq

//Get a map from key phrases to total score across all texts
[<Import("keyTotalScore","./keyTotalScore.js")>]
let GetKeyTotalScore: unit -> KeyScore[] = jsNative
let keyTotalScore = 
    GetKeyTotalScore()
    |> Seq.map( fun ks -> ks.K, ks.S)
    |> Map.ofSeq

let spaceRegex = new System.Text.RegularExpressions.Regex(@"\s+");
let NormalizeText ( text : string ) =
    spaceRegex.Replace( text, " " ).Trim().ToLower()


//Update proper
let update (msg:Msg) (model:Model) =
    match msg with
    | DoQuery -> 
        //normalize user input
        let normalized = model.Query |> NormalizeText
        //find all key phrase matches on it
        let keyMatches =
            keyTotalScore
            |> Seq.map (|KeyValue|)
            |> Seq.choose( fun (k,v) -> if normalized.IndexOf(k) <> -1 then Some(k,v) else None )
            |> Seq.sortByDescending snd
            |> Seq.truncate 30 //TODO: arbitrary return N most freq
 
        let results =
            keyMatches
            //at this level we just aggregate information we might use
            |> Seq.collect( fun (key,totalScore)-> 
               keyIdScore.[key] //idScores of all texts the key appeared in
               |> Seq.collect( fun idScore -> 
                    idAuthorOrder.[idScore.I] //list of authorOrders for the text
                    //consider this step like joining various DB tables
                    //for each author we get all keys, text-level scores for those keys, and total scores for those keys (across documents)
                    |> Seq.map( fun authorOrder -> key, authorOrder, idScore.I, idScore.S, totalScore)
                    //|> Seq.distinct //equality semantics seem to be failing in Fable here. Could be the Pojo attribute
                    )
              )
            //at this level we explore different metrics
            //normalize keyword score for particular reviewer's paper by total for that keyword in corpus
            //then normalize by the author's position in author order
            |> Seq.map( fun (k, authorOrder, id, score, total) -> 
                let title = idTitle.[id]
                let searchResult =
                    {
                        Keyphrase=k; 
                        Author= authorOrder.A; 
                        Score = System.Math.Round( (float(score)/float(total)) / (float(authorOrder.O)+1.0) ,2 );
                        Title = title 
                    }
                //manually hashing to correct equality semantics
                hash searchResult, searchResult 
            )
            |> Seq.distinctBy fst //distinct using our manual hash
            |> Seq.map snd        //pop off the manual hash
            //at this level we group on keyword and sort matches descending by score
            // |> Seq.groupBy(fun searchResult -> searchResult.Keyphrase )
            // |> Seq.map( fun (k,v) -> 
            //     { 
            //         Keyphrase = k;
            //         Results = v |> Seq.sortByDescending( fun searchResult -> searchResult.Score ) |> ResizeArray 
            //     } )
            //|> Seq.truncate 30 //TODO: arbitrary return N most freq
            |> ResizeArray

        {model with Results=results}
    | UpdateQuery(query) ->
        { model with Query = query}

// VIEW (rendered with React)

//React table mojo, see here https://github.com/fable-compiler/fable-react/issues/61
// Helpers for dynamic typing
let inline (~%) x = createObj x
let inline (=>) k v = k ==> v

let reactTable ( data : ResizeArray<SearchResult> ) =
    let columns =
        [| %["Header" => "Keyphrase"
             "accessor" =>  "Keyphrase"]
           %["Header" => "Author"
             "accessor" => "Author"]
           %["Header" => "Score"
             "accessor" => "Score"]
           %["Header" => "Title"
             "accessor" => "Title"]
            |]
    ofImport "default" "react-table" %["data"=>data; "columns"=>columns; "pivotBy" => [|"Keyphrase"|] ; "defaultSorted"=> [|%[ "id" => "Score"; "desc" => true  ] |] ] []


let view model dispatch =

  div []
      [ 
        div [] [
            h1 [] [ str "JEDM Reviewer Finder"]
            p [] [ str "Enter your query here. The best way is to open the PDF of the submission and copy/paste the text here. You may wish to omit the reference section." ]
            textarea [
                ClassName "new-query"
                DefaultValue model.Query
                Size 100000.0
                Style [
                    Width "100%"
                    Height "600px"
                ] 
                OnInput (fun ev ->  UpdateQuery (!!ev.target?value) |> dispatch )
            ] []
        ]
               
        div [] [ 
            button [ OnClick (fun _ ->  DoQuery |> dispatch) ] [ str "Get Reviewers!" ]
            //str (string model) 
            ]
        br [] //padding
        div [] [
            p [] [ str "Your results appear here. Use your judgment to expand keywords that correspond with important keywords in the submission." ]
            reactTable model.Results
        ]
      ]
Program.mkSimple init update view
|> Program.withReact "elmish-app"
|> Program.withConsoleTrace
|> Program.run