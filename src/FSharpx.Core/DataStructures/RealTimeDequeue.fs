﻿//originally published by Julien
// original implementation taken from http://lepensemoi.free.fr/index.php/2010/02/05/real-time-double-ended-queue

//jf -- added rev, ofSeq, ofSeqC, lookup, append, appendC, update, remove, tryUpdate, tryRemove
//   -- added standardized C of 2 for singleton, empty, and ofSeq based on my reading of Okasaki

//pattern discriminators Cons, Snoc, and Nil

module FSharpx.DataStructures.RealTimeDequeue

open LazyList
open System.Collections
open System.Collections.Generic

type RealTimeDequeue<'a>(c : int, frontLength : int, front : LazyList<'a>,  streamFront : LazyList<'a>,  rBackLength : int, rBack : LazyList<'a>, streamRBack : LazyList<'a>) = 
   
    member private this.c = c

    member private this.frontLength = frontLength

    member private this.front = front

    member private this.streamFront = streamFront

    member private this.rBackLength = rBackLength

    member private this.rBack = rBack

    member private this.streamRBack = streamRBack

    static member private length (q : RealTimeDequeue<'a>) = q.frontLength + q.rBackLength

    static member private exec1 : LazyList<'a> -> LazyList<'a> = function
        | LazyList.Cons(x, s) -> s
        | s -> s

    static member private exec2 (x : LazyList<'a>) : LazyList<'a> = (RealTimeDequeue.exec1 >> RealTimeDequeue.exec1) x

    static member private check (q : RealTimeDequeue<'a>) =

        let rec rotateDrop c f j r =

            let rec rotateRev c = function
                | LazyList.Nil, r, a -> LazyList.append (lLrev r) a
                | LazyList.Cons(x, f), r, a ->
                    let a' = lLdrop c r
                    let b' = LazyList.append (LazyList.take c r) a |> lLrev
                    LazyList.cons x (rotateRev c (f, a', b'))

            if j < c then
              rotateRev c (f, lLdrop j r, LazyList.empty)
            else
              match f with
              | LazyList.Cons(x, f') -> LazyList.cons x (rotateDrop c f' (j-c) (lLdrop c r))
              | _ -> failwith "should not get there"

        let n = RealTimeDequeue.length q
        if q.frontLength > q.c * q.rBackLength + 1 then
          let i= n / 2
          let j = n - i
          let f' = LazyList.take i q.front
          let r' = rotateDrop q.c q.rBack i q.front
          new RealTimeDequeue<'a>(q.c, i, f', f', j, r', r')
        elif q.rBackLength > q.c * q.frontLength + 1 then
          let j = n / 2
          let i = n - j
          let r' = LazyList.take j q.rBack
          let f' = rotateDrop q.c q.front j q.rBack
          new RealTimeDequeue<'a>(q.c, i, f', f', j, r', r')
        else
          q

    static member private check2 q =
        let n = RealTimeDequeue.length q
        if q.frontLength > q.c * q.rBackLength + 1 then
            let i= n / 2
            let j = n - i
            let f' = LazyList.take i q.front
            let r' = lLdrop i q.front |> lLrev |> LazyList.append q.rBack
            new RealTimeDequeue<'a>(q.c, i, f', f', j, r', r')
        elif q.rBackLength > q.c * q.frontLength + 1 then
            let j = n / 2
            let i = n - j
            let r' = LazyList.take j q.rBack
            let f' = lLdrop j q.rBack |> lLrev |> LazyList.append q.front
            new RealTimeDequeue<'a>(q.c, i, f', f', j, r', r')
        else
            new RealTimeDequeue<'a>(q.c, q.frontLength, q.front, q.front, q.rBackLength, q.rBack, q.rBack)

    static member internal AppendC cC (xs:RealTimeDequeue<'a>) (ys:RealTimeDequeue<'a>) =
        let front2 = xs.frontLength + xs.rBackLength
        let front3 = xs.frontLength + xs.rBackLength + ys.frontLength
        let back2 = ys.frontLength + ys.rBackLength
        let back3 = xs.rBackLength + ys.frontLength + ys.rBackLength
        match (front2, front3, back2, back3) with 
        | a, b, c, d when (abs(xs.frontLength - d) <= abs(a - c)) && (abs(xs.frontLength - d) <= abs(a - ys.rBackLength) && (xs.frontLength > 0)) -> 
            new RealTimeDequeue<'a>(cC, xs.frontLength, xs.front, LazyList.empty, 
                (xs.rBackLength + ys.frontLength + ys.rBackLength), (LazyList.append ys.rBack (LazyList.append  (lLrev ys.front) xs.rBack)), LazyList.empty)
            |> RealTimeDequeue.check2
        | a, b, c, d when (abs(a - c) <= abs(xs.frontLength - d)) && (abs(a - c) <= abs(a - ys.rBackLength)) -> 
            new RealTimeDequeue<'a>(cC, (xs.frontLength + xs.rBackLength), (LazyList.append xs.front (lLrev xs.rBack)), LazyList.empty, 
                (ys.frontLength + ys.rBackLength), (LazyList.append ys.rBack (lLrev ys.front)), LazyList.empty)
            |> RealTimeDequeue.check2
        | a, b, c, d ->
            new RealTimeDequeue<'a>(cC, (xs.frontLength + xs.rBackLength + ys.frontLength), (LazyList.append (LazyList.append xs.front (lLrev xs.rBack)) ys.front), LazyList.empty, 
                ys.rBackLength, ys.rBack, LazyList.empty)
            |> RealTimeDequeue.check2

    static member internal Empty c =
        new RealTimeDequeue<'a>(c, 0, (LazyList.empty), (LazyList.empty), 0, (LazyList.empty), (LazyList.empty)) 

    member this.Lookup (i:int) =
        match frontLength, front, rBackLength, rBack with
        | lenF, front, lenR, rear when i > (lenF + lenR - 1) -> raise Exceptions.OutOfBounds
        | lenF, front, lenR, rear when i < lenF -> 
            let rec loopF = function 
                | xs, i'  when i' = 0 -> LazyList.head xs
                | xs, i' -> loopF ((LazyList.tail xs), (i' - 1))
            loopF (front, i)
        | lenF, front, lenR, rear ->  
            let rec loopF = function 
                | xs, i'  when i' = 0 -> LazyList.head xs
                | xs, i' -> loopF ((LazyList.tail xs), (i' - 1))
            loopF (rear, ((lenR - (i - lenF)) - 1))

    member this.TryLookup (i:int) =
        match frontLength, front, rBackLength, rBack with
        | lenF, front, lenR, rear when i > (lenF + lenR - 1) -> None
        | lenF, front, lenR, rear when i < lenF -> 
            let rec loopF = function 
                | xs, i'  when i' = 0 -> Some(LazyList.head xs)
                | xs, i' -> loopF ((LazyList.tail xs), (i' - 1))
            loopF (front, i)
        | lenF, front, lenR, rear ->  
            let rec loopF = function 
                | xs, i'  when i' = 0 -> Some(LazyList.head xs)
                | xs, i' -> loopF ((LazyList.tail xs), (i' - 1))
            loopF (rear, ((lenR - (i - lenF)) - 1))

    static member internal OfCatListsC c (xs : 'a list) (ys : 'a list) =
        new RealTimeDequeue<'a>(c, xs.Length, (LazyList.ofList xs), LazyList.empty, ys.Length, (LazyList.ofList (List.rev ys)), LazyList.empty)
        |> RealTimeDequeue.check2

    static member internal OfCatSeqsC c (xs : 'a seq) (ys : 'a seq) =
        new RealTimeDequeue<'a>(c, (Seq.length xs), (LazyList.ofSeq xs), LazyList.empty, (Seq.length ys), (lLrev (LazyList.ofSeq ys)), LazyList.empty)
        |> RealTimeDequeue.check2
   
    static member internal OfSeqC c (xs:seq<'a>) = 
        new RealTimeDequeue<'a>(c, (Seq.length xs), (LazyList.ofSeq xs), LazyList.empty, 0, LazyList.empty, LazyList.empty)
        |> RealTimeDequeue.check2

    member this.Remove (i:int) =
        match frontLength, front, rBackLength, rBack with
        | lenF, front, lenR, rear when i > (lenF + lenR - 1) -> raise Exceptions.OutOfBounds
        | lenF, front, lenR, rear when i < lenF -> 
            let newFront = 
                if (i = 0) then LazyList.tail front
                else 
                    let left, right = lLsplit front i
                    LazyList.append (List.rev left |> LazyList.ofList) right

            new RealTimeDequeue<'a>(c, (lenF - 1), newFront, LazyList.empty, lenR, rear, LazyList.empty)
            |> RealTimeDequeue.check2

        | lenF, front, lenR, rear ->  
            let n = lenR - (i - lenF) - 1
            let newRear = 
                if (n = 0) then LazyList.tail rear
                else 
                    let left, right = lLsplit rear n
                    LazyList.append (List.rev left |> LazyList.ofList) right

            new RealTimeDequeue<'a>(c, lenF, front, LazyList.empty, (lenR - 1), newRear, LazyList.empty)
            |> RealTimeDequeue.check2

    member this.TryRemove (i:int) =
        match frontLength, front, rBackLength, rBack with
        | lenF, front, lenR, rear when i > (lenF + lenR - 1) -> None
        | lenF, front, lenR, rear when i < lenF -> 
            let newFront = 
                if (i = 0) then LazyList.tail front
                else 
                    let left, right = lLsplit front i
                    LazyList.append (List.rev left |> LazyList.ofList) right

            let z = new RealTimeDequeue<'a>(c, (lenF - 1), newFront, LazyList.empty, lenR, rear, LazyList.empty) |> RealTimeDequeue.check2
            Some(z)

        | lenF, front, lenR, rear ->  
            let n = lenR - (i - lenF) - 1
            let newRear = 
                if (n = 0) then LazyList.tail rear
                else 
                    let left, right = lLsplit rear n
                    LazyList.append (List.rev left |> LazyList.ofList) right
        
            let z = new RealTimeDequeue<'a>(c, lenF, front, LazyList.empty, (lenR - 1), newRear, LazyList.empty) |> RealTimeDequeue.check2
            Some(z)

    member this.Rev = 
        new RealTimeDequeue<'a>(c, rBackLength, rBack, streamRBack, frontLength, front, streamFront)

    member this.Update (i:int) (y: 'a) =
        match frontLength, front, rBackLength, rBack with
        | lenF, front, lenR, rear when i > (lenF + lenR - 1) -> raise Exceptions.OutOfBounds
        | lenF, front, lenR, rear when i < lenF -> 
            let newFront = 
                if (i = 0) then LazyList.cons y (LazyList.tail front)
                else 
                    let left, right = lLsplit front i
                    LazyList.append (List.rev left |> LazyList.ofList) (LazyList.cons y right)

            new RealTimeDequeue<'a>(c, lenF, newFront, LazyList.empty, lenR, rear, LazyList.empty)
            |> RealTimeDequeue.check2

        | lenF, front, lenR, rear ->  
            let n = lenR - (i - lenF) - 1
            let newRear = 
                if (n = 0) then LazyList.cons y (LazyList.tail rear)
                else 
                    let left, right = lLsplit rear n
                    LazyList.append (List.rev left |> LazyList.ofList) (LazyList.cons y right)
        
            new RealTimeDequeue<'a>(c, lenF, front, LazyList.empty, lenR, newRear, LazyList.empty)
            |> RealTimeDequeue.check2

    member this.TryUpdate (i:int) (y: 'a) =
        match frontLength, front, rBackLength, rBack with
        | lenF, front, lenR, rear when i > (lenF + lenR - 1) -> None
        | lenF, front, lenR, rear when i < lenF -> 
            let newFront = 
                if (i = 0) then LazyList.cons y (LazyList.tail front)
                else 
                    let left, right = lLsplit front i
                    LazyList.append (List.rev left |> LazyList.ofList) (LazyList.cons y right)

            let z = new RealTimeDequeue<'a>(c, lenF, newFront, LazyList.empty, lenR, rear, LazyList.empty) |> RealTimeDequeue.check2
            Some(z)

        | lenF, front, lenR, rear ->  
            let n = lenR - (i - lenF) - 1
            let newRear = 
                if (n = 0) then LazyList.cons y (LazyList.tail rear)
                else 
                    let left, right = lLsplit rear n
                    LazyList.append (List.rev left |> LazyList.ofList) (LazyList.cons y right)
        
            let z = new RealTimeDequeue<'a>(c, lenF, front, LazyList.empty, lenR, newRear, LazyList.empty) |> RealTimeDequeue.check2
            Some(z)

    with
    interface IDequeue<'a> with

        member this.Cons x =
            new RealTimeDequeue<'a>(this.c, (this.frontLength+1), (LazyList.cons x this.front), (RealTimeDequeue.exec1 this.streamFront), this.rBackLength, this.rBack, (RealTimeDequeue.exec1 this.streamRBack)) 
            |> RealTimeDequeue.check :> _ 
   
        member this.Head() =
            match this.front, this.rBack with
            | LazyList.Nil, LazyList.Nil -> raise Exceptions.Empty
            | LazyList.Nil, LazyList.Cons(x, _) -> x
            | LazyList.Cons(x, _), _ -> x

        member this.Init() = 
            match this.front, this.rBack with
            | LazyList.Nil, LazyList.Nil -> raise Exceptions.Empty
            | _, LazyList.Nil -> RealTimeDequeue.Empty this.c :> _ 
            | _, LazyList.Cons(x, xs) ->
                new RealTimeDequeue<'a>(this.c, this.frontLength, this.front, (RealTimeDequeue.exec2 this.streamFront), (this.rBackLength-1), xs, (RealTimeDequeue.exec2 this.streamRBack))
                |> RealTimeDequeue.check :> _ 
          
        member this.IsEmpty() =  
            ((this.frontLength = 0) && (this.rBackLength = 0))

        member this.Last() = 
            match this.front, this.rBack with
            | LazyList.Nil, LazyList.Nil -> raise Exceptions.Empty
            | _, LazyList.Cons(x, _) ->  x
            | LazyList.Cons(x, _), LazyList.Nil-> x

        member this.Length() = RealTimeDequeue.length this

        member this.Tail() =
            match this.front, this.rBack with
            | LazyList.Nil, LazyList.Nil -> raise Exceptions.Empty
            | LazyList.Nil, LazyList.Cons(x, _) -> RealTimeDequeue.Empty this.c :> _ 
            | LazyList.Cons(x, xs), _ ->
               new RealTimeDequeue<'a>(this.c, (this.frontLength-1), xs, (RealTimeDequeue.exec2 this.streamFront), this.rBackLength, this.rBack, (RealTimeDequeue.exec2 this.streamRBack))
                |> RealTimeDequeue.check :> _

        member this.Snoc x = 
            new RealTimeDequeue<'a>(this.c, this.frontLength, this.front, (RealTimeDequeue.exec1 this.streamFront), (this.rBackLength+1), (LazyList.cons x this.rBack), (RealTimeDequeue.exec1 this.streamRBack))
            |> RealTimeDequeue.check :> _

        member this.Uncons() =  
            match this.front, this.rBack with
            | LazyList.Nil, LazyList.Nil -> raise Exceptions.Empty
            | _, _ -> this.Head(), this.Tail() :> _

        member this.Unsnoc() =  
            match this.front, this.rBack with
            | LazyList.Nil, LazyList.Nil -> raise Exceptions.Empty
            | _, _ -> this.Init() :> _, this.Last()
          
    interface IEnumerable<'a> with

        member this.GetEnumerator() = 
            let e = seq {
                  yield! front
                  yield! (lLrev this.rBack)  }
            e.GetEnumerator()

        member this.GetEnumerator() = (this :> _ seq).GetEnumerator() :> IEnumerator

    ///returns a new deque with the element added to the beginning
    member this.Cons x = ((this :> 'a IDequeue).Cons x) :?> RealTimeDequeue<'a>

    ///returns the first element
    member this.Head() = (this :> 'a IDequeue).Head()

    ///returns a new deque of the elements before the last element
    member this.Init() = ((this :> 'a IDequeue).Init()) :?> RealTimeDequeue<'a>

    ///returns true if the deque has no elements
    member this.IsEmpty() = (this :> 'a IDequeue).IsEmpty()

    ///returns the last element
    member this.Last() = (this :> 'a IDequeue).Last()

    ///returns the count of elememts
    member this.Length() = (this :> 'a IDequeue).Length()

    ///returns a new deque with the element added to the end
    member this.Snoc x = ((this :> 'a IDequeue).Snoc x) :?> RealTimeDequeue<'a>

    ///returns a new deque of the elements trailing the first element
    member this.Tail() = ((this :> 'a IDequeue).Tail()) :?> RealTimeDequeue<'a>

    ///returns the first element and tail
    member this.Uncons() = 
        let x, xs = (this :> 'a IDequeue).Uncons() 
        x, xs :?> RealTimeDequeue<'a>

    ///returns init and the last element
    member this.Unsnoc() = 
        let xs, x = (this :> 'a IDequeue).Unsnoc() 
        xs :?> RealTimeDequeue<'a>, x

//pattern discriminator

let private getDequeCons (q : RealTimeDequeue<'a>) = 
    if q.IsEmpty() then None
    else Some(q.Head(), q.Tail())

let private getDequeSnoc (q : RealTimeDequeue<'a>) = 
    if q.IsEmpty() then None
    else Some(q.Init(), q.Last())

let (|Cons|Nil|) q = match getDequeCons q with Some(a,b) -> Cons(a,b) | None -> Nil

let (|Snoc|Nil|) q = match getDequeSnoc q with Some(a,b) -> Snoc(a,b) | None -> Nil

let private stndC = 2

///returns a deque of the two deques concatenated
///front-back stream ratio constant defaulted to 2
let append (xs : RealTimeDequeue<'a>) (ys : RealTimeDequeue<'a>) = RealTimeDequeue.AppendC stndC xs ys

///returns a deque of the two deques concatenated
///c is front-back stream ratio constant, should be at least 2
let appendC c (xs : RealTimeDequeue<'a>) (ys : RealTimeDequeue<'a>) = RealTimeDequeue.AppendC c xs ys

///returns a new deque with the element added to the beginning
let inline cons (x : 'a) (q : RealTimeDequeue<'a>) = q.Cons x 

//returns deque of no elements
///c is front-back stream ration constant, should be at least 2
let empty c = RealTimeDequeue.Empty c

///returns the first element
let inline head (q : RealTimeDequeue<'a>) = q.Head()

///returns a new deque of the elements before the last element
let inline init (q : RealTimeDequeue<'a>) = q.Init() 

///returns true if the deque has no elements
let inline isEmpty (q : RealTimeDequeue<'a>) = q.IsEmpty()

///returns the last element
let inline last (q : RealTimeDequeue<'a>) = q.Last()

///returns the count of elememts
let inline length (q : RealTimeDequeue<'a>) = q.Length()

///returns option element by index
let inline lookup i (q : RealTimeDequeue<'a>) = q.Lookup i

///returns option element by index
let inline tryLookup i (q : RealTimeDequeue<'a>) = q.TryLookup i

///returns a deque of the two lists concatenated
///front-back stream ratio constant defaulted to 2
let ofCatLists xs ys = RealTimeDequeue.OfCatListsC stndC xs ys

///returns a deque of the two lists concatenated
///c is front-back stream ration constant, should be at least 2
let ofCatListsC c xs ys = RealTimeDequeue.OfCatListsC c xs ys

///returns a deque of the two seqs concatenated
///front-back stream ratio constant defaulted to 2
let ofCatSeqs xs ys = RealTimeDequeue.OfCatSeqsC stndC xs ys

///returns a deque of the two seqs concatenated
///c is front-back stream ratio constant, should be at least 2
let ofCatSeqsC c xs ys = RealTimeDequeue.OfCatSeqsC c xs ys

///returns a deque of the seq
///front-back stream ratio constant defaulted to 2
let ofSeq xs = RealTimeDequeue.OfSeqC stndC xs

///returns a deque of the seq
///c is front-back stream ratio constant, should be at least 2
let ofSeqC c xs = RealTimeDequeue.OfSeqC c xs

//returns deque with element removed by index
let inline remove i (q : RealTimeDequeue<'a>) = q.Remove i

//returns deque reversed
let inline rev (q : RealTimeDequeue<'a>) = q.Rev

//returns option deque with element removed by index
let inline tryRemove i (q : RealTimeDequeue<'a>) = q.TryRemove i

//returns a deque of one element
///front-back stream ratio constant defaulted to 2
let singleton x = empty stndC |> cons x  

//returns a deque of one element
///c is front-back stream ratio constant, should be at least 2
let singletonC c x = empty c |> cons x  

///returns a new deque with the element added to the end
let inline snoc (x : 'a) (q : RealTimeDequeue<'a>) = (q.Snoc x) 

///returns a new deque of the elements trailing the first element
let inline tail (q : RealTimeDequeue<'a>) = q.Tail() 

///returns the first element and tail
let inline uncons (q : RealTimeDequeue<'a>) = q.Uncons()

///returns init and the last element
let inline unsnoc (q : RealTimeDequeue<'a>) = q.Unsnoc()

//returns deque with element updated by index
let inline update i y (q : RealTimeDequeue<'a>) = q.Update i y

//returns option deque with element updated by index
let inline tryUpdate i y (q : RealTimeDequeue<'a>) = q.TryUpdate i y